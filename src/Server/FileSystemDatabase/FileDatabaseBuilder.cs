// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VsChromium.Core;
using VsChromium.Core.Linq;
using VsChromium.Core.Win32.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase {
  public class FileDatabaseBuilder {
    /// <summary>
    /// Note: For debugging purposes only.
    /// </summary>
    private bool OutputDiagnostics = false;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private Dictionary<FileName, FileInfo> _files;
    private DirectoryName[] _directoryNames;

    public FileDatabaseBuilder(IFileContentsFactory fileContentsFactory, IProgressTrackerFactory progressTrackerFactory) {
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public IFileDatabase Build(IFileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot) {
      return ComputeFileDatabase((FileDatabase)previousFileDatabase, newSnapshot);
    }

    /// <summary>
    /// Prepares this instance for searches by computing various snapshots from
    /// the previous <see cref="FileDatabase"/> snapshot and the new current
    /// <see cref="FileSystemTreeSnapshot"/> instance.
    /// </summary>
    private FileDatabase ComputeFileDatabase(FileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot) {
      if (previousFileDatabase == null)
        throw new ArgumentNullException("previousFileDatabase");

      if (newSnapshot == null)
        throw new ArgumentNullException("newSnapshot");

      // Compute list of files from tree
      ComputeFileCollection(newSnapshot);

      // Merge old state in new state
      TransferUnchangedFileContents(previousFileDatabase);

      // Load file contents into newState
      ReadMissingFileContents();

      return CreateFileDatabse();
    }

    private FileDatabase CreateFileDatabse() {
      Logger.Log("Freezing FileDatabase state.");
      var sw = Stopwatch.StartNew();

      var files = _files.ToDictionary(x => x.Key, x => x.Value.FileData);
      var fileNames = _files.Select(x => x.Key).ToArray();
      var directoryNames = _directoryNames;
      // Note: Partitioning evenly ensures that each processor used by PLinq will deal with 
      // a partition of equal "weight". In this case, we make sure each partition contains
      // not only the same amount of files, but also (as close to as possible) the same
      // amount of "bytes". For example, if we have 100 files totaling 32MB and 4 processors,
      // we will end up with 4 partitions of (exactly) 25 files totalling (approximately) 8MB each.
      var filesWithContents = files.Values
        .Where(x => x.Contents != null)
        .ToList()
        .PartitionEvenly(fileData => fileData.Contents.ByteLength)
        .SelectMany(x => x)
        .ToArray();


      sw.Stop();
      Logger.Log("Done freezing FileDatabase state in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();

      if (OutputDiagnostics) {
        // Note: For diagnostic only as this can be quite slow.
        filesWithContents
          .GroupBy(x => {
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
      return new FileDatabase(_fileContentsFactory, files, fileNames, directoryNames, filesWithContents);
    }

    private void ComputeFileCollection(FileSystemTreeSnapshot snapshot) {
      Logger.Log("Computing list of searchable files from FileSystemTree.");
      var sw = Stopwatch.StartNew();

      var directories = FileSystemSnapshotVisitor.GetDirectories(snapshot).ToList();
      var directoryNames = directories
        .AsParallel()
        .Select(x => x.Value.DirectoryName)
        .ToArray();
      var files = directories
        .AsParallel()
        .SelectMany(x => x.Value.Files.Select(y => new KeyValuePair<IProject, FileName>(x.Key, y)))
        .Select(x => new FileInfo(new FileData(x.Value, null), x.Key.IsFileSearchable(x.Value)))
        .ToDictionary(x => x.FileData.FileName, x => x);

      sw.Stop();
      Logger.Log("Done computing list of searchable files from FileSystemTree in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();

      _files = files;
      _directoryNames = directoryNames;
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
          .ForAll(oldFileData => _files[oldFileData.FileName].FileData.UpdateContents(oldFileData.Contents));
      }

      Logger.Log("Done checking for {0:n0} out of date files in {1:n0} msec.", commonOldFiles.Count,
                 sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    /// <summary>
    /// Reads the content of all file entries that have no content (yet). Returns the # of files read from disk.
    /// </summary>
    private void ReadMissingFileContents() {
      Logger.Log("Loading file contents from disk.");
      var sw = Stopwatch.StartNew();

      using (var progress = _progressTrackerFactory.CreateTracker(_files.Count)) {
        _files.Values
          .AsParallel()
          .ForAll(x => {
            progress.Step((i, n) => string.Format("Reading file {0:n0} of {1:n0}: {2}", i, n, x.FileData.FileName.GetFullName()));
            if (x.IsSearchable && x.FileData.Contents == null) {
              x.FileData.UpdateContents(_fileContentsFactory.GetFileContents(x.FileData.FileName.GetFullName()));
            }
          });
      }

      sw.Stop();
      Logger.Log("Done loading file contents from disk: loaded {0:n0} files in {1:n0} msec.",
        _files.Count,
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

      return oldState.Files.Values.Intersect(_files.Values.Select(x => x.FileData), FileDataComparer.Instance);
    }

    private struct FileInfo {
      private readonly FileData _fileData;
      private readonly bool _isSearchable;

      public FileInfo(FileData fileData, bool isSearchable) {
        _fileData = fileData;
        _isSearchable = isSearchable;
      }

      public FileData FileData { get { return _fileData; } }
      public bool IsSearchable { get { return _isSearchable; } }
    }

    private class FileDataComparer : IEqualityComparer<FileData>, IComparer<FileData> {
      private static readonly FileDataComparer _instance = new FileDataComparer();

      private FileDataComparer() {
      }

      public static FileDataComparer Instance { get { return _instance; } }

      public bool Equals(FileData x, FileData y) {
        return x.FileName.Equals(y.FileName);
      }

      public int GetHashCode(FileData x) {
        return x.FileName.GetHashCode();
      }

      public int Compare(FileData x, FileData y) {
        return x.FileName.CompareTo(y.FileName);
      }
    }
  }
}
