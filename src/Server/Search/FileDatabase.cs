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
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemTreeSnapshot"/> snapshot.
  /// </summary>
  public class FileDatabase {
    /// <summary>
    /// Note: For debugging purposes only.
    /// </summary>
    private bool OutputDiagnostics = false;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private Dictionary<FileName, FileData> _files;
    private DirectoryName[] _directoryNames;
    private FileName[] _fileNames;
    private FileData[] _filesWithContents;
    private bool _frozen;

    public FileDatabase(IFileContentsFactory fileContentsFactory, IProgressTrackerFactory progressTrackerFactory) {
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

    public static FileDatabase Empty(IFileContentsFactory fileContentsFactory, IProgressTrackerFactory progressTrackerFactory) {
      var result = new FileDatabase(fileContentsFactory, progressTrackerFactory);
      result.Freeze(new IntermediateState());
      return result;
    }

    /// <summary>
    /// Prepares this instance for searches by computing various snapshots from
    /// the previous <see cref="FileDatabase"/> snapshot and the new current
    /// <see cref="FileSystemTreeSnapshot"/> instance.
    /// </summary>
    public void ComputeState(FileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot) {
      if (_frozen)
        throw new InvalidOperationException("FileDatabase is frozen.");

      if (previousFileDatabase == null)
        throw new ArgumentNullException("previousFileDatabase");

      if (newSnapshot == null)
        throw new ArgumentNullException("newSnapshot");

      // Compute list of files from tree
      var state = ComputeFileCollection(newSnapshot);

      // Merge old state in new state
      TransferUnchangedFileContents(state, previousFileDatabase);

      // Load file contents into newState
      ReadMissingFileContents(state);

      // Done with new state!
      Freeze(state);

      if (OutputDiagnostics) {
        // Note: For diagnostic only as this can be quite slow.
        FilesWithContents
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
    }

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

      fileData.UpdateContents(_fileContentsFactory.GetFileContents(changedFile.Item2.GetFullName()));
    }

    private static IntermediateState ComputeFileCollection(FileSystemTreeSnapshot snapshot) {
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

      return new IntermediateState {
        Files = files,
        DirectoryNames = directoryNames,
      };
    }

    /// <summary>
    /// Return the <see cref="FileData"/> instance associated to <paramref name="filename"/> or null if not present.
    /// </summary>
    private FileData GetFileData(FileName filename) {
      FileData result;
      _files.TryGetValue(filename, out result);
      return result;
    }

    private void Freeze(IntermediateState state) {
      if (_frozen)
        throw new InvalidOperationException("FileDatabase is already frozen.");

      Logger.Log("Freezing FileDatabase state.");
      var sw = Stopwatch.StartNew();

      _files = state.Files.ToDictionary(x => x.Key, x => x.Value.FileData);
      _fileNames = _files.Select(x => x.Key).ToArray();
      _directoryNames = state.DirectoryNames;
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

      _frozen = true;

      sw.Stop();
      Logger.Log("Done freezing FileDatabase state in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    private void TransferUnchangedFileContents(IntermediateState state, FileDatabase oldState) {
      Logger.Log("Checking for out of date files.");
      var sw = Stopwatch.StartNew();

      IList<FileData> commonOldFiles = GetCommonFiles(state, oldState).ToArray();
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
          .ForAll(oldFileData => state.Files[oldFileData.FileName].FileData.UpdateContents(oldFileData.Contents));
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

    private static IEnumerable<FileData> GetCommonFiles(IntermediateState state, FileDatabase oldState) {
      if (state.Files.Count == 0 || oldState._files.Count == 0)
        return Enumerable.Empty<FileData>();

      return oldState._files.Values.Intersect(state.Files.Values.Select(x => x.FileData), FileDataComparer.Instance);
    }

    /// <summary>
    /// Reads the content of all file entries that have no content (yet). Returns the # of files read from disk.
    /// </summary>
    /// <param name="state"></param>
    private void ReadMissingFileContents(IntermediateState state) {
      Logger.Log("Loading file contents from disk.");
      var sw = Stopwatch.StartNew();

      using (var progress = _progressTrackerFactory.CreateTracker(state.Files.Count)) {
        state.Files.Values
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
        state.Files.Count,
        sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    private class IntermediateState {
      public IntermediateState() {
        Files = new Dictionary<FileName, FileInfo>();
        DirectoryNames = new DirectoryName[0];
      }
      public Dictionary<FileName, FileInfo> Files { get; set; }
      public DirectoryName[] DirectoryNames { get; set; }
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
