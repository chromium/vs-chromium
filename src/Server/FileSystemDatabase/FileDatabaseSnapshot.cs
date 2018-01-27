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
    private readonly IReadOnlyMap<FileName, FileWithContents> _files;
    private readonly IList<FileName> _fileNames;
    private readonly IReadOnlyMap<DirectoryName, DirectoryData> _directories;
    private readonly IList<IFileContentsPiece> _fileContentsPieces;
    private readonly long _searchableFileCount;

    public FileDatabaseSnapshot(IReadOnlyMap<FullPath, string> projectHashes,
      IReadOnlyMap<FileName, FileWithContents> files, IList<FileName> fileNames,
      IReadOnlyMap<DirectoryName, DirectoryData> directories, IList<IFileContentsPiece> fileContentsPieces,
      long searchableFileCount) {
      _projectHashes = projectHashes;
      _files = files;
      _fileNames = fileNames;
      _directories = directories;
      _fileContentsPieces = fileContentsPieces;
      _searchableFileCount = searchableFileCount;
    }

    public IReadOnlyMap<FullPath, string> ProjectHashes => _projectHashes;
    public IReadOnlyMap<FileName, FileWithContents> Files => _files;
    public IReadOnlyMap<DirectoryName, DirectoryData> Directories => _directories;
    public IList<FileName> FileNames => _fileNames;
    public IList<IFileContentsPiece> FileContentsPieces => _fileContentsPieces;
    public long SearchableFileCount => _searchableFileCount;

    public IEnumerable<FileExtract> GetFileExtracts(FileName filename, IEnumerable<FilePositionSpan> spans, int maxLength) {
      var fileData = GetFileData(filename);
      if (fileData == null)
        return Enumerable.Empty<FileExtract>();

      var contents = fileData.Contents;
      if (contents == null)
        return Enumerable.Empty<FileExtract>();

      return contents.GetFileExtracts(maxLength, spans);
    }

    public bool IsContainedInSymLink(FileSystemName name) {
      return IsContainedInSymLinkHelper(_directories, name);
    }

    public static bool IsContainedInSymLinkHelper(IReadOnlyMap<DirectoryName, DirectoryData> directories, FileSystemName name) {
      var directoryName = (name as DirectoryName) ?? name.Parent;
      if (directoryName == null)
        return false;

      DirectoryData directoryData;
      if (!directories.TryGetValue(directoryName, out directoryData))
        return false;

      if (directoryData.IsSymLink)
        return true;

      var parent = directoryName.Parent;
      if (parent == null)
        return false;

      return IsContainedInSymLinkHelper(directories, parent);
    }

    public static bool IsContainedInSymLinkHelper(IDictionary<DirectoryName, DirectoryData> directories, FileSystemName name) {
      var directoryName = (name as DirectoryName) ?? name.Parent;
      if (directoryName == null)
        return false;

      DirectoryData directoryData;
      if (!directories.TryGetValue(directoryName, out directoryData))
        return false;

      if (directoryData.IsSymLink)
        return true;

      var parent = directoryName.Parent;
      if (parent == null)
        return false;

      return IsContainedInSymLinkHelper(directories, parent);
    }

    /// <summary>
    /// Return the <see cref="FileWithContents"/> instance associated to <paramref name="filename"/> or null if not present.
    /// </summary>
    private FileWithContents GetFileData(FileName filename) {
      FileWithContents result;
      Files.TryGetValue(filename, out result);
      return result;
    }
  }
}
