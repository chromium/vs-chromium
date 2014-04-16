// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VsChromium.Core;
using VsChromium.Core.Collections;
using VsChromium.Core.Linq;
using VsChromium.Core.Win32.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemTree;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemTreeInternal"/> snapshot.
  /// </summary>
  public class FileDatabase {
    /// <summary>
    /// Note: For debugging purposes only.
    /// </summary>
    private bool OutputDiagnostics = false;
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private SortedArray<FileData> _files;
    private DirectoryName[] _directoryNames;
    private FileName[] _fileNames;
    private FileData[] _filesWithContents;
    private bool _frozen;

    public FileDatabase(IProjectDiscovery projectDiscovery, IFileContentsFactory fileContentsFactory, IProgressTrackerFactory progressTrackerFactory) {
      _projectDiscovery = projectDiscovery;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    /// <summary>
    /// Returns the list of filenames suitable for file name search.
    /// </summary>
    public ICollection<FileName> FileNames { get { return _fileNames; } }

    /// <summary>
    /// Returns the list of directory names suitable for directory name search.
    /// </summary>
    public ICollection<DirectoryName> DirectoryNames { get { return _directoryNames; } }

    /// <summary>
    /// Retunrs the list of files with text contents suitable for text search.
    /// </summary>
    public ICollection<FileData> FilesWithContents { get { return _filesWithContents; } }

    /// <summary>
    /// Return the <see cref="FileData"/> instance associated to <paramref name="filename"/> or null if not present.
    /// </summary>
    public FileData GetFileData(FileName filename) {
      int index = _files.BinaraySearch(filename, (data, name) => data.FileName.CompareTo(name));
      if (index < 0)
        return null;
      return _files[index];
    }

    /// <summary>
    /// Prepares this instance for searches by computing various snapshots from
    /// the previous <see cref="FileDatabase"/> snapshot and the new current
    /// <see cref="FileSystemTreeInternal"/> instance.
    /// </summary>
    public void ComputeState(FileDatabase previousFileDatabase, FileSystemTreeInternal newTree) {
      if (_frozen) 
        throw new InvalidOperationException("FileDatabase is frozen.");

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

    private void ComputeFileCollection(FileSystemTreeInternal tree) {
      Logger.Log("Computing list of searchable files from FileSystemTree.");
      var sw = Stopwatch.StartNew();

      var files = new List<FileData>();
      var directoryNames = new List<DirectoryName>();

      var visitor = new FileSystemTreeVisitor(tree);
      visitor.VisitFile = fileEntry => files.Add(new FileData(fileEntry.FileName, null));
      visitor.VisitDirectory = directoryEntry => {
        if (!directoryEntry.IsRoot)
          directoryNames.Add(directoryEntry.DirectoryName);
      };
      visitor.Visit();

      // Store internally in sorted arrays
      _files = new SortedArray<FileData>(files.OrderBy(x => x.FileName).ToArray());
      _directoryNames = directoryNames.OrderBy(x => x).ToArray();

      sw.Stop();
      Logger.Log("Done computing list of searchable files from FileSystemTree in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    public void Freeze() {
      if (_frozen)
        throw new InvalidOperationException("FileDatabase is already frozen.");

      Logger.Log("Freezing FileDatabase state.");
      var sw = Stopwatch.StartNew();

      if (_files == null) {
        _files = new SortedArray<FileData>();
        _directoryNames = new DirectoryName[0];
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
      _filesWithContents = _files
        .Where(x => x.Contents != null)
        .ToList()
        .PartitionEvenly(fileData => fileData.Contents.ByteLength)
        .SelectMany(x => x)
        .ToArray();

      _fileNames = _files.Select(x => x.FileName).ToArray();

      _frozen = true;

      sw.Stop();
      Logger.Log("Done freezing FileDatabase state in {0:n0} msec.", sw.ElapsedMilliseconds);
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
          .ForAll(oldFileData => GetFileData(oldFileData.FileName).UpdateContents(oldFileData.Contents));
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
      if (_files.Count == 0 || oldState._files.Count == 0)
        return Enumerable.Empty<FileData>();

      return oldState._files.Intersect(_files, FileDataComparer.Instance);
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

      sw.Stop();
      Logger.Log("Done loading file contents from disk: loaded {0:n0} files in {1:n0} msec.", filesToRead.Count,
                 sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    private ICollection<FileData> GetMissingFileContentsList() {
      Logger.Log("Computing list of files to read from disk.");
      var sw = Stopwatch.StartNew();

      var filesToRead = _files
        .Where(x => x.Contents == null)
        .Where(x => _projectDiscovery.IsFileSearchable(x.FileName))
        .ToArray();

      sw.Stop();
      Logger.Log("Done computing list of files to read from disk in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
      return filesToRead;
    }

    private class FileDataComparer : IEqualityComparer<FileData>, IComparer<FileData> {
      private static readonly FileDataComparer _instance = new FileDataComparer();

      private FileDataComparer() {
      }

      public static FileDataComparer Instance { get { return _instance; }}

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
