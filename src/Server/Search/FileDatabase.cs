// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VsChromium.Core;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Win32.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemTree;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.Search {
  public class FileDatabase {
    /// <summary>
    /// Note: For debugging purposes only.
    /// </summary>
    private bool OutputDiagnostics = false;
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private IList<DirectoryName> _directoryNames;
    private IList<FileName> _fileNames;
    private Dictionary<FileName, FileData> _files;
    private IList<FileData> _filesWithContents;
    private bool _frozen;

    public FileDatabase(
      IProjectDiscovery projectDiscovery,
      IFileSystemNameFactory fileSystemNameFactory,
      IFileContentsFactory fileContentsFactory,
      IProgressTrackerFactory progressTrackerFactory) {
      _projectDiscovery = projectDiscovery;
      _fileSystemNameFactory = fileSystemNameFactory;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public bool Frozen { get { return _frozen; } }

    public Dictionary<FileName, FileData> Files { get { return _files; } }

    public IList<FileName> FileNames { get { return _fileNames; } }

    public IList<DirectoryName> DirectoryNames { get { return _directoryNames; } }

    public IList<FileData> FilesWithContents { get { return _filesWithContents; } }

    public void ComputeState(FileDatabase previousFileDatabase, FileSystemTreeInternal newTree) {
      if (previousFileDatabase == null)
        throw new ArgumentNullException("previousFileDatabase");

      if (newTree == null)
        throw new ArgumentNullException("newTree");

      // Compute list of files from tree
      ComputeFileCollection(newTree);

      // Merge old state in new state
      TransferUnchangedFileContents(previousFileDatabase);

      // Load file contents into newState
      ReadMissingFileContents();

      // Done with new state!
      Freeze();

      if (OutputDiagnostics)
      {
        // Note: For diagnostic only as this can be quite slow.
        FilesWithContents
          .GroupBy(x =>
          {
            var ext = Path.GetExtension(x.FileName.Name);
            if (string.IsNullOrEmpty(ext))
              return new { Type = "Filename", Value = x.FileName.Name };
            else
              return new { Type = "Extension", Value = ext };
          })
          .OrderByDescending(x => x.Aggregate(0L, (s, f) => s + f.Contents.ByteLength))
          .ForAll(g => {
            var byteLength = g.Aggregate(0L, (s, f) => s + f.Contents.ByteLength);
            Logger.Log("{0} \"{1}\": {2:n0} files totalling {3:n0} bytes.", g.Key.Type, g.Key.Value, g.Count(), byteLength);
          });
      }
    }

    public void Freeze() {
      Logger.Log("Freezing FileDatabase state.");
      var sw = Stopwatch.StartNew();

      if (_files == null) {
        _files = new Dictionary<FileName, FileData>();
        _directoryNames = new List<DirectoryName>();
      }

      //
      // Compute additional data for fast search!
      //

      Debug.Assert(_files != null);
      Debug.Assert(_directoryNames != null);

      // Note: Partitioning evenly ensures that each processor used by PLinq will deal with 
      // a partition of equal "weight". In this case, we make sure each partition contains
      // not only the same amount of files, but also (as close to as possible) the same
      // amount of "bytes". For example, if we have 100 files totaling 32MB and 4 processors,
      // we will end up with 4 partitions of (exactly) 25 files totalling (approximately) 8MB each.
      _filesWithContents = _files.Values
        .Where(x => x.Contents != null)
        .ToList()
        .PartitionEvenly(fileData => fileData.Contents.ByteLength)
        .SelectMany(x => x)
        .ToArray();

      _fileNames = _files.Keys
        .ToArray();

      _frozen = true;

      sw.Stop();
      Logger.Log("Done freezing FileDatabase state in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    private void ComputeFileCollection(FileSystemTreeInternal tree) {
      Logger.Log("Computing list of searchable files from FileSystemTree.");
      var sw = Stopwatch.StartNew();

      _files = new Dictionary<FileName, FileData>();
      _directoryNames = new List<DirectoryName>();

      var visitor = new FileSystemTreeVisitor(tree);
      visitor.VisitFile = fileEntry => _files.Add(fileEntry.Name, new FileData(fileEntry.Name, null));
      visitor.VisitDirectory = directoryEntry => {
        if (!directoryEntry.IsRoot)
          _directoryNames.Add(directoryEntry.Name);
      };
      visitor.Visit();

      sw.Stop();
      Logger.Log("Done computing list of searchable files from FileSystemTree in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    private void TransferUnchangedFileContents(FileDatabase oldState) {
      Logger.Log("Checking for out of date files.");
      var sw = Stopwatch.StartNew();

      IList<FileData> commonOldFiles = GetCommonFiles(oldState).ToArray();
      using (var progress = _progressTrackerFactory.CreateTracker(commonOldFiles.Count)) {
        commonOldFiles
          .AsParallel()
          .Where(oldFileData => {
            progress.Step(
              (i, n) =>
              string.Format("Checking file timestamp {0:n0} of {1:n0}: {2}", i, n,
                            oldFileData.FileName.GetFullName()));
            return IsFileContentsUpToDate(oldFileData);
          })
          .ForAll(oldFileData => _files[oldFileData.FileName].UpdateContents(oldFileData.Contents));
      }

      Logger.Log("Done checking for {0:n0} out of date files in {1:n0} msec.", commonOldFiles.Count,
                 sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    private static bool IsFileContentsUpToDate(FileData oldFileData) {
      // TODO(rpaquay): The following File.Exists and File.GetLastWriteTimUtc are expensive operations.
      //  Given we have FileSystemChanged events when files change on disk, we could be smarter here
      // and avoid 99% of these checks in common cases.
      var fi = new SlimFileInfo(oldFileData.FileName.GetFullName());
      if (fi.Exists) {
        var contents = oldFileData.Contents;
        if (contents != null) {
          if (fi.LastWriteTimeUtc == contents.UtcLastModified)
            return true;
        }
      }
      return false;
    }

    private IEnumerable<FileData> GetCommonFiles(FileDatabase oldState) {
      if (_files.Count == 0 || oldState.Files.Count == 0)
        return Enumerable.Empty<FileData>();

      return oldState.Files.Values.Intersect(_files.Values, new FileDataComparer());
    }

    /// <summary>
    /// Reads the content of all file entries that have no content (yet). Returns the # of files read from disk.
    /// </summary>
    private void ReadMissingFileContents() {
      var filesToRead = GetMissingFileContentsList();

      Logger.Log("Loading file contents from disk.");
      var sw = Stopwatch.StartNew();

      using (var progress = _progressTrackerFactory.CreateTracker(filesToRead.Count)) {
        filesToRead
          .AsParallel()
          .ForAll(x => {
            progress.Step(
              (i, n) => string.Format("Reading file {0:n0} of {1:n0}: {2}", i, n, x.FileName.GetFullName()));
            x.UpdateContents(_fileContentsFactory.GetFileContents(x.FileName.GetFullName()));
          });
      }

#if false
  // Note: This can be very slow (logging of 100,000+ files).
      _files
        .Where(x => x.Value.Contents != null)
        //.OrderByDescending(x => x.Value.Contents.ByteLength)
        .OrderBy(x => x.Key.RelativePathName.RelativeName)
        .ForAll(x => Logger.Log("File {0}: {1:n0} bytes.", x.Key.RelativePathName, x.Value.Contents.ByteLength));
#endif

      sw.Stop();
      Logger.Log("Done loading file contents from disk: loaded {0:n0} files in {1:n0} msec.", filesToRead.Count,
                 sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    private IList<FileData> GetMissingFileContentsList() {
      Logger.Log("Computing list of files to read from disk.");
      var sw = Stopwatch.StartNew();

      // Read all files in parallel
      var filesToRead = _files
        .Select(x => x.Value)
        .AsParallel()
        .Where(x => x.Contents == null)
        .Where(x => _projectDiscovery.IsFileSearchable(x.FileName))
        .ToArray();

      sw.Stop();
      Logger.Log("Done computing list of files to read from disk in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
      return filesToRead;
    }

    private class FileDataComparer : IEqualityComparer<FileData> {
      public bool Equals(FileData x, FileData y) {
        return x.FileName.Equals(y.FileName);
      }

      public int GetHashCode(FileData x) {
        return x.FileName.GetHashCode();
      }
    }
  }
}
