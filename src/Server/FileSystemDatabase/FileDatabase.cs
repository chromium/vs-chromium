// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemTreeSnapshot"/> snapshot.
  /// </summary>
  public class FileDatabase : IFileDatabase {
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IDictionary<FileName, FileData> _files;
    private readonly IDictionary<DirectoryName, DirectoryData> _directories;
    private readonly ICollection<FileData> _filesWithContents;

    public FileDatabase(IFileContentsFactory fileContentsFactory,
                        IDictionary<FileName, FileData> files,
                        IDictionary<DirectoryName, DirectoryData> directories,
                        ICollection<FileData> filesWithContents) {
      _fileContentsFactory = fileContentsFactory;
      _files = files;
      _directories = directories;
      _filesWithContents = filesWithContents;
    }

    /// <summary>
    /// Returns the list of files with their associated <see cref="FileData"/>.
    /// Note that some instances of <see cref="FileData"/> may have their <see
    /// cref="FileData.Contents"/> property set to null.
    /// </summary>
    public IDictionary<FileName, FileData> Files { get { return _files; } }

    /// <summary>
    /// Returns the list of filenames suitable for file name search.
    /// </summary>
    public ICollection<FileName> FileNames { get { return _files.Keys; } }

    /// <summary>
    /// Returns the list of directory names suitable for directory name search.
    /// </summary>
    public ICollection<DirectoryName> DirectoryNames { get { return _directories.Keys; } }

    /// <summary>
    /// Retunrs the list of files with text contents suitable for text search.
    /// </summary>
    public ICollection<FileData> FilesWithContents { get { return _filesWithContents; } }

    public IEnumerable<FileExtract> GetFileExtracts(FileName filename, IEnumerable<FilePositionSpan> spans) {
      var fileData = GetFileData(filename);
      if (fileData == null)
        return Enumerable.Empty<FileExtract>();

      var contents = fileData.Contents;
      if (contents == null)
        return Enumerable.Empty<FileExtract>();

      return contents.GetFileExtracts(spans);
    }

    public void UpdateFileContents(Tuple<IProject, FileName> changedFile) {
      // Concurrency: We may update the FileContents value of some entries, but
      // we ensure we do not update collections and so on. So, all in all, it is
      // safe to make this change "lock free".
      var fileData = GetFileData(changedFile.Item2);
      if (fileData == null)
        return;

      if (!changedFile.Item1.IsFileSearchable(changedFile.Item2))
        return;

      fileData.UpdateContents(_fileContentsFactory.GetFileContents(changedFile.Item2.FullPath));
    }

    public bool IsContainedInSymLink(DirectoryName name) {
      DirectoryData entry;
      if (!_directories.TryGetValue(name, out entry))
        return false;

      if (entry.DirectoryEntry.IsSymLink)
        return true;

      var parent = entry.DirectoryName.Parent;
      if (parent == null)
        return false;

      return IsContainedInSymLink(parent);
    }

    /// <summary>
    /// Return the <see cref="FileData"/> instance associated to <paramref name="filename"/> or null if not present.
    /// </summary>
    private FileData GetFileData(FileName filename) {
      FileData result;
      Files.TryGetValue(filename, out result);
      return result;
    }
  }
}
