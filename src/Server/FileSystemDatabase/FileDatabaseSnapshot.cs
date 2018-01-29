// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemSnapshot"/> snapshot.
  /// </summary>
  public class FileDatabaseSnapshot : IFileDatabaseSnapshot {
    private readonly IReadOnlyMap<FullPath, string> _projectHashes;
    private readonly IDictionary<FileName, FileWithContents> _files;
    private readonly IList<FileName> _fileNames;
    private readonly IReadOnlyMap<DirectoryName, DirectoryData> _directories;
    private readonly IList<FileContentsPiece> _fileContentsPieces;
    private readonly long _searchableFileCount;

    public FileDatabaseSnapshot(IReadOnlyMap<FullPath, string> projectHashes,
      IDictionary<FileName, FileWithContents> files, IList<FileName> fileNames,
      IReadOnlyMap<DirectoryName, DirectoryData> directories, IList<FileContentsPiece> fileContentsPieces,
      long searchableFileCount) {
      _projectHashes = projectHashes;
      _files = files;
      _fileNames = fileNames;
      _directories = directories;
      _fileContentsPieces = fileContentsPieces;
      _searchableFileCount = searchableFileCount;
    }

    public IReadOnlyMap<FullPath, string> ProjectHashes => _projectHashes;
    public IDictionary<FileName, FileWithContents> Files => _files;
    public IReadOnlyMap<DirectoryName, DirectoryData> Directories => _directories;
    public IList<FileName> FileNames => _fileNames;
    public IList<FileContentsPiece> FileContentsPieces => _fileContentsPieces;
    public long SearchableFileCount => _searchableFileCount;

    public IEnumerable<FileExtract> GetFileExtracts(FileName filename, IEnumerable<FilePositionSpan> spans, int maxLength) {
      var contents = GetFileContents(filename);
      if (contents == null) {
        return Enumerable.Empty<FileExtract>();
      }

      return contents.GetFileExtracts(maxLength, spans);
    }

    public bool IsContainedInSymLink(DirectoryName name) {
      return IsContainedInSymLinkHelper(_directories, name);
    }

    public static bool IsContainedInSymLinkHelper(IReadOnlyMap<DirectoryName, DirectoryData> directories, DirectoryName name) {
      if (name == null)
        return false;

      DirectoryData directoryData;
      if (!directories.TryGetValue(name, out directoryData))
        return false;

      if (directoryData.IsSymLink)
        return true;

      var parent = name.Parent;
      if (parent == null)
        return false;

      return IsContainedInSymLinkHelper(directories, parent);
    }

    public static bool IsContainedInSymLinkHelper(IReadOnlyMap<DirectoryName, DirectoryData> directories, FileName name) {
      return IsContainedInSymLinkHelper(directories, name.Parent);
    }

    public static bool IsContainedInSymLinkHelper(IDictionary<DirectoryName, DirectoryData> directories, FileName name) {
      return IsContainedInSymLinkHelper(directories, name.Parent);
    }

    public static bool IsContainedInSymLinkHelper(IDictionary<DirectoryName, DirectoryData> directories, DirectoryName name) {
      if (name == null)
        return false;

      DirectoryData directoryData;
      if (!directories.TryGetValue(name, out directoryData))
        return false;

      if (directoryData.IsSymLink)
        return true;

      var parent = name.Parent;
      if (parent == null)
        return false;

      return IsContainedInSymLinkHelper(directories, parent);
    }

    /// <summary>
    /// Return the <see cref="FileContents"/> instance associated to <paramref name="filename"/> or null if not present.
    /// </summary>
    private FileContents GetFileContents(FileName filename) {
      FileWithContents result;
      if (!Files.TryGetValue(filename, out result)) {
        return null;
      }
      return result.Contents;
    }
  }
}
