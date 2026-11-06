// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Protocol.Generated;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemDatabase.Builder;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.JsonRpc;
using VsChromium.Server.Operations;
using VsChromium.Server.Search;

namespace VsChromium.Server.Projects {
  /// <summary>
  /// Computes per-directory disk usage from the in-memory <see cref="FileSystemSnapshot"/> and
  /// <see cref="FileDatabaseSnapshot"/>, enumerates excluded children of a browsed directory, runs
  /// on-demand background measurements of excluded directories, and edits project ignore sections.
  /// </summary>
  public sealed class ProjectDiskUsageService : IDisposable {
    /// <summary>
    /// Brief wait on newly started measurements so tiny excluded directories complete in a single
    /// request without blocking large directories.
    /// </summary>
    private static readonly TimeSpan InitialMeasureWait = TimeSpan.FromMilliseconds(15);

    private readonly IFileSystem _fileSystem;
    private readonly IFileSystemSnapshotManager _snapshotManager;
    private readonly ISearchEngine _searchEngine;

    private readonly object _cacheLock = new object();
    private readonly Dictionary<FullPath, CachedProjectSizes> _sizeCache = new Dictionary<FullPath, CachedProjectSizes>();

    private readonly object _measureLock = new object();
    private readonly Dictionary<FullPath, MeasurementJob> _measurements = new Dictionary<FullPath, MeasurementJob>();
    private bool _disposed;

    public ProjectDiskUsageService(
      IFileSystem fileSystem,
      IFileSystemSnapshotManager snapshotManager,
      ISearchEngine searchEngine) {
      _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
      _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
      _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));

      _snapshotManager.SnapshotScanFinished += OnSnapshotScanFinished;
      _searchEngine.FilesLoaded += OnFilesLoaded;
    }

    public void Dispose() {
      _snapshotManager.SnapshotScanFinished -= OnSnapshotScanFinished;
      _searchEngine.FilesLoaded -= OnFilesLoaded;

      List<MeasurementJob> jobs;
      lock (_measureLock) {
        if (_disposed) {
          return;
        }
        _disposed = true;
        jobs = _measurements.Values.ToList();
        _measurements.Clear();
      }

      foreach (var job in jobs) {
        job.Cancel();
      }

      lock (_cacheLock) {
        _sizeCache.Clear();
      }
    }

    public DirectorySizes GetDirectorySizes(DirectoryPathParams parameters) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      var projectSnapshot = FindProject(snapshot, parameters.ProjectPath);
      var targetDir = FindIndexedDirectory(projectSnapshot, parameters.RelativePath, out var normalizedRelPath);
      var database = _searchEngine.CurrentFileDatabaseSnapshot as FileDatabaseSnapshot;

      var projectSizes = GetOrComputeProjectSizes(snapshot.Version, projectSnapshot, database, out var computeMs);
      var dirTotals = projectSizes.DirectoryTotals.TryGetValue(targetDir, out var foundTotals)
        ? foundTotals
        : default;

      var children = new List<DirectorySizeEntry>(targetDir.ChildDirectories.Count + targetDir.ChildFiles.Count + 8);
      var indexedDirNames = new HashSet<string>(SystemPathComparer.NameComparer);
      foreach (var childDir in targetDir.ChildDirectories) {
        var name = childDir.DirectoryName.Name;
        indexedDirNames.Add(name);
        var childTotals = projectSizes.DirectoryTotals.TryGetValue(childDir, out var ct) ? ct : default;
        var entry = CreateEntry(
          name: name,
          isDirectory: true,
          excluded: false,
          measureState: MeasureState.Done,
          totals: childDir.IsSymLink ? default : childTotals,
          pattern: ProjectFileEditor.BuildExactPattern(normalizedRelPath, name, isDirectory: true));
        if (childDir.IsSymLink) {
          // Note: The totals of the link are those of the files seen through it (its target).
          entry.SymbolicLink = DescribeSymbolicLink(snapshot, childDir.DirectoryName.FullPath, isDirectory: true, childTotals);
        }
        children.Add(entry);
      }

      var indexedFileNames = new HashSet<string>(SystemPathComparer.NameComparer);
      var childFiles = targetDir.ChildFiles;
      var childMetadata = targetDir.ChildFileMetadata;
      var hasDbRange = TryGetDirectoryRange(database, targetDir, out var dbRange, out var dbAligned);
      var dbContents = database?.ContentsArray;

      for (var i = 0; i < childFiles.Count; i++) {
        var fileName = childFiles[i];
        indexedFileNames.Add(fileName.Name);
        var contents = GetFileContents(database, dbContents, hasDbRange, dbRange, dbAligned, fileName, i);
        var fileTotals = ComputeFileTotals(childMetadata[i], contents);
        var isLink = targetDir.IsSymLinkFile(i);
        var entry = CreateEntry(
          name: fileName.Name,
          isDirectory: false,
          excluded: false,
          measureState: MeasureState.Done,
          totals: isLink ? default : fileTotals,
          pattern: ProjectFileEditor.BuildExactPattern(normalizedRelPath, fileName.Name, isDirectory: false));
        if (isLink) {
          entry.SymbolicLink = DescribeSymbolicLink(snapshot, fileName.FullPath, isDirectory: false, fileTotals);
        }
        children.Add(entry);
      }

      AppendExcludedDiskChildren(
        projectSnapshot.Project,
        targetDir,
        normalizedRelPath,
        indexedDirNames,
        indexedFileNames,
        children);

      children.Sort(CompareEntries);

      var rootDisplayName = targetDir.DirectoryName.IsAbsoluteName
        ? projectSnapshot.Project.RootPath.FileName
        : targetDir.DirectoryName.Name;
      string parentOfTarget = null;
      string targetPattern = null;
      if (!string.IsNullOrEmpty(normalizedRelPath)) {
        var slash = normalizedRelPath.LastIndexOf('/');
        parentOfTarget = slash >= 0 ? normalizedRelPath.Substring(0, slash) : "";
        targetPattern = ProjectFileEditor.BuildExactPattern(parentOfTarget, targetDir.DirectoryName.Name, isDirectory: true);
      }

      var editableFile = ProjectFileEditor.TryGetEditableProjectFile(projectSnapshot.Project, out var projectFilePath, out _)
        ? projectFilePath.Value
        : null;

      return new DirectorySizes {
        ProjectPath = projectSnapshot.Project.RootPath.Value,
        RelativePath = normalizedRelPath,
        Totals = CreateEntry(
          name: rootDisplayName,
          isDirectory: true,
          excluded: false,
          measureState: MeasureState.Done,
          totals: dirTotals,
          pattern: targetPattern),
        Children = children,
        ProjectFile = editableFile,
        SnapshotVersion = snapshot.Version,
        ComputeMs = computeMs,
      };
    }

    public async ValueTask<DirectoryMeasurement> MeasureDirectoryAsync(
      MeasureDirectoryParams parameters,
      CancellationToken cancellationToken) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      var projectSnapshot = FindProject(snapshot, parameters.ProjectPath);
      var (fullPath, normalizedRelPath) = ResolveMeasurableDirectory(projectSnapshot, parameters.RelativePath);

      MeasurementJob job;
      var startedNew = false;
      lock (_measureLock) {
        if (_disposed) {
          throw new RecoverableErrorException("Server is shutting down.");
        }

        if (_measurements.TryGetValue(fullPath, out var existing)) {
          if (existing.State == MeasureState.Running && parameters.Restart != true) {
            return existing.ToProtocol();
          }
          if (existing.State == MeasureState.Done && parameters.Restart != true) {
            return existing.ToProtocol();
          }
          existing.Cancel();
        }

        job = new MeasurementJob(normalizedRelPath);
        _measurements[fullPath] = job;
        startedNew = true;
      }

      if (startedNew) {
        job.Start(() => RunMeasurement(job, fullPath));
        try {
          using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
          timeoutCts.CancelAfter(InitialMeasureWait);
          await job.Completion.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
          // Still running in the background; return current progress snapshot.
        }
      }

      cancellationToken.ThrowIfCancellationRequested();
      return job.ToProtocol();
    }

    public DirectoryMeasurement CancelMeasure(DirectoryPathParams parameters) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      var projectSnapshot = FindProject(snapshot, parameters.ProjectPath);
      var (fullPath, normalizedRelPath) = ValidateRelativeSubPath(projectSnapshot.Project.RootPath, parameters.RelativePath);

      lock (_measureLock) {
        if (_measurements.TryGetValue(fullPath, out var existing)) {
          existing.Cancel();
          return existing.ToProtocol();
        }
      }

      return new DirectoryMeasurement {
        RelativePath = normalizedRelPath,
        State = MeasureState.NotMeasured,
      };
    }

    public AddIgnorePatternResult AddIgnorePattern(AddIgnorePatternParams parameters) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      var projectSnapshot = FindProject(snapshot, parameters.ProjectPath);
      if (!ProjectFileEditor.TryGetEditableProjectFile(
            projectSnapshot.Project,
            out var projectFilePath,
            out var sourceExplorerSection)) {
        throw new RecoverableErrorException(
          $"Project \"{projectSnapshot.Project.RootPath.Value}\" has no editable \"{VsChromium.Core.Configuration.ConfigurationFileNames.ProjectFileName}\" file.");
      }

      var sectionName = ProjectFileEditor.GetSectionName(
        projectSnapshot.Project,
        parameters.Section,
        sourceExplorerSection);
      var result = ProjectFileEditor.AddPatternToFile(projectFilePath, sectionName, parameters.Pattern);
      if (result.Added) {
        Logger.LogInfo("Added pattern \"{0}\" to [{1}] in \"{2}\".", result.Line, result.SectionName, result.ProjectFile);
      }
      return result;
    }

    private void OnSnapshotScanFinished(object sender, SnapshotScanResult e) {
      PruneSizeCache();
    }

    private void OnFilesLoaded(object sender, FilesLoadedResult e) {
      PruneSizeCache();
    }

    private void PruneSizeCache() {
      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      lock (_cacheLock) {
        if (_sizeCache.Count == 0) {
          return;
        }
        var staleKeys = new List<FullPath>();
        foreach (var kvp in _sizeCache) {
          var currentProject = snapshot.ProjectRoots.FirstOrDefault(p => p.Project.RootPath.Equals(kvp.Key));
          if (currentProject == null ||
              !ReferenceEquals(currentProject.Directory, kvp.Value.RootDirectory) ||
              !ReferenceEquals(database, kvp.Value.Database)) {
            staleKeys.Add(kvp.Key);
          }
        }
        foreach (var key in staleKeys) {
          _sizeCache.Remove(key);
        }
      }
    }

    private CachedProjectSizes GetOrComputeProjectSizes(
      int snapshotVersion,
      ProjectRootSnapshot projectSnapshot,
      FileDatabaseSnapshot database,
      out double computeMs) {
      var rootPath = projectSnapshot.Project.RootPath;
      lock (_cacheLock) {
        if (_sizeCache.TryGetValue(rootPath, out var cached) &&
            ReferenceEquals(cached.RootDirectory, projectSnapshot.Directory) &&
            ReferenceEquals(cached.Database, database)) {
          computeMs = 0;
          return cached;
        }
      }

      var sw = Stopwatch.StartNew();
      var totalsByDir = ComputeDirectoryTotals(projectSnapshot.Directory, database);
      sw.Stop();
      computeMs = sw.Elapsed.TotalMilliseconds;

      var created = new CachedProjectSizes(snapshotVersion, projectSnapshot.Directory, database, totalsByDir);
      lock (_cacheLock) {
        _sizeCache[rootPath] = created;
      }
      return created;
    }

    private static Dictionary<DirectorySnapshot, EntryTotals> ComputeDirectoryTotals(
      DirectorySnapshot root,
      FileDatabaseSnapshot database) {
      var order = new List<DirectorySnapshot>(256) { root };
      for (var i = 0; i < order.Count; i++) {
        var subDirs = order[i].ChildDirectories;
        for (var j = 0; j < subDirs.Count; j++) {
          order.Add(subDirs[j]);
        }
      }

      var totalsByDir = new Dictionary<DirectorySnapshot, EntryTotals>(order.Count, ReferenceEqualityComparer.Instance);
      var dbContents = database?.ContentsArray;

      for (var idx = order.Count - 1; idx >= 0; idx--) {
        var dir = order[idx];
        var totals = default(EntryTotals);

        // Note: The symbolic links are not counted (their targets are counted where they are, if
        // indexed): the totals of a directory link (the files seen through it) are kept for the
        // link entry, not added to its parent.
        var childDirs = dir.ChildDirectories;
        for (var j = 0; j < childDirs.Count; j++) {
          if (!childDirs[j].IsSymLink && totalsByDir.TryGetValue(childDirs[j], out var childTotals)) {
            totals.Add(childTotals);
          }
        }

        var childFiles = dir.ChildFiles;
        var metadata = dir.ChildFileMetadata;
        var hasRange = TryGetDirectoryRange(database, dir, out var range, out var aligned);
        for (var j = 0; j < childFiles.Count; j++) {
          if (dir.IsSymLinkFile(j)) {
            continue;
          }
          var contents = GetFileContents(database, dbContents, hasRange, range, aligned, childFiles[j], j);
          totals.Add(ComputeFileTotals(metadata[j], contents));
        }

        totalsByDir[dir] = totals;
      }

      return totalsByDir;
    }

    private static bool TryGetDirectoryRange(
      FileDatabaseSnapshot database,
      DirectorySnapshot dir,
      out FileRange range,
      out bool aligned) {
      if (database != null && database.Directories.TryGetValue(dir.DirectoryName, out range)) {
        var childFiles = dir.ChildFiles;
        if (range.Count == childFiles.Count) {
          var dbNames = database.FileNameArray;
          aligned = true;
          for (var i = 0; i < childFiles.Count; i++) {
            if (!ReferenceEquals(dbNames[range.Start + i].Name, childFiles[i].Name) &&
                !SystemPathComparer.EqualsNames(dbNames[range.Start + i].Name, childFiles[i].Name)) {
              aligned = false;
              break;
            }
          }
          return true;
        }
        aligned = false;
        return true;
      }

      range = default;
      aligned = false;
      return false;
    }

    private static FileContents GetFileContents(
      FileDatabaseSnapshot database,
      FileContents[] dbContents,
      bool hasRange,
      FileRange range,
      bool aligned,
      FileName fileName,
      int fileIndexInDirectory) {
      if (database == null || dbContents == null) {
        return null;
      }
      if (aligned) {
        return Volatile.Read(ref dbContents[range.Start + fileIndexInDirectory]);
      }
      if (hasRange && database.TryGetFileIndex(fileName, out var dbIndex)) {
        return Volatile.Read(ref dbContents[dbIndex]);
      }
      return null;
    }

    private static EntryTotals ComputeFileTotals(FileMetadata metadata, FileContents contents) {
      var length = contents != null && contents.FileLength >= 0
        ? contents.FileLength
        : metadata.Length;
      if (length < 0) {
        length = 0;
      }

      var isSearchable = FileDatabaseBuilder.HasContents(contents);
      return new EntryTotals {
        TotalBytes = length,
        SearchableBytes = isSearchable ? length : 0,
        FileCount = 1,
        SearchableFileCount = isSearchable ? 1 : 0,
      };
    }

    private void AppendExcludedDiskChildren(
      IProject project,
      DirectorySnapshot targetDir,
      string normalizedRelPath,
      HashSet<string> indexedDirNames,
      HashSet<string> indexedFileNames,
      List<DirectorySizeEntry> children) {
      var dirFullPath = project.RootPath.Combine(targetDir.DirectoryName.RelativePath);
      IList<DirectoryEntry> diskEntries;
      try {
        diskEntries = _fileSystem.GetDirectoryEntries(dirFullPath);
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Failed to enumerate directory \"{0}\" for excluded entries.", dirFullPath);
        return;
      }

      for (var i = 0; i < diskEntries.Count; i++) {
        var entry = diskEntries[i];
        if (string.IsNullOrEmpty(entry.Name)) {
          continue;
        }
        var childRelPath = targetDir.DirectoryName.RelativePath.CreateChild(entry.Name);
        if (entry.IsDirectory) {
          if (indexedDirNames.Contains(entry.Name) || project.DirectoryFilter.Include(childRelPath)) {
            continue;
          }
          var childFullPath = project.RootPath.Combine(childRelPath);
          DirectoryMeasurement measurement = null;
          lock (_measureLock) {
            if (_measurements.TryGetValue(childFullPath, out var job)) {
              measurement = job.ToProtocol();
            }
          }

          var state = measurement?.State ?? MeasureState.NotMeasured;
          var totalBytes = measurement?.TotalBytes ?? 0;
          var fileCount = measurement?.FileCount ?? 0;
          children.Add(new DirectorySizeEntry {
            Name = entry.Name,
            IsDirectory = true,
            Excluded = true,
            MeasureState = state,
            TotalBytes = totalBytes,
            SearchableBytes = 0,
            NotSearchableBytes = totalBytes,
            FileCount = fileCount,
            SearchableFileCount = 0,
            Pattern = ProjectFileEditor.BuildExactPattern(normalizedRelPath, entry.Name, isDirectory: true),
          });
        } else if (entry.IsFile) {
          if (indexedFileNames.Contains(entry.Name) || project.FileFilter.Include(childRelPath)) {
            continue;
          }
          var length = Math.Max(0L, entry.Metadata.Length);
          children.Add(new DirectorySizeEntry {
            Name = entry.Name,
            IsDirectory = false,
            Excluded = true,
            MeasureState = MeasureState.Done,
            TotalBytes = length,
            SearchableBytes = 0,
            NotSearchableBytes = length,
            FileCount = 1,
            SearchableFileCount = 0,
            Pattern = ProjectFileEditor.BuildExactPattern(normalizedRelPath, entry.Name, isDirectory: false),
          });
        }
      }
    }

    private void RunMeasurement(MeasurementJob job, FullPath rootDirectoryPath) {
      var stack = new Stack<FullPath>();
      stack.Push(rootDirectoryPath);
      var token = job.Token;

      try {
        while (stack.Count > 0) {
          token.ThrowIfCancellationRequested();
          var current = stack.Pop();

          IList<DirectoryEntry> entries;
          try {
            entries = _fileSystem.GetDirectoryEntries(current);
          }
          catch (Exception e) {
            if (current.Equals(rootDirectoryPath)) {
              job.Fail(e.Message);
              return;
            }
            Logger.LogWarn(e, "Skipping inaccessible directory \"{0}\" during size measurement.", current);
            continue;
          }

          for (var i = 0; i < entries.Count; i++) {
            if ((i & 0xFF) == 0) {
              token.ThrowIfCancellationRequested();
            }
            var entry = entries[i];
            if (entry.IsDirectory) {
              if (entry.IsSymLink) {
                continue;
              }
              job.AddDirectory();
              stack.Push(new FullPath(PathHelpers.CombinePaths(current.Value, entry.Name)));
            } else if (entry.IsFile) {
              job.AddFile(Math.Max(0L, entry.Metadata.Length));
            }
          }
        }

        job.Complete();
      }
      catch (OperationCanceledException) {
        job.MarkCancelled();
      }
      catch (Exception e) {
        job.Fail(e.Message);
      }
    }

    private static DirectorySizeEntry CreateEntry(
      string name,
      bool isDirectory,
      bool excluded,
      MeasureState measureState,
      EntryTotals totals,
      string pattern) {
      return new DirectorySizeEntry {
        Name = name,
        IsDirectory = isDirectory,
        Excluded = excluded,
        MeasureState = measureState,
        TotalBytes = totals.TotalBytes,
        SearchableBytes = totals.SearchableBytes,
        NotSearchableBytes = Math.Max(0L, totals.TotalBytes - totals.SearchableBytes),
        FileCount = totals.FileCount,
        SearchableFileCount = totals.SearchableFileCount,
        Pattern = pattern,
      };
    }

    private static int CompareEntries(DirectorySizeEntry a, DirectorySizeEntry b) {
      var rankA = GetSortRank(a);
      var rankB = GetSortRank(b);
      if (rankA != rankB) {
        return rankA.CompareTo(rankB);
      }

      if (rankA == 0) {
        var bySize = b.TotalBytes.CompareTo(a.TotalBytes);
        if (bySize != 0) {
          return bySize;
        }
      }

      if (a.IsDirectory != b.IsDirectory) {
        return a.IsDirectory ? -1 : 1;
      }
      return SystemPathComparer.CompareNames(a.Name, b.Name);
    }

    /// <summary>
    /// The sized entries first (largest first), then the symbolic links (not counted), then the
    /// excluded directories not measured, then the other entries of size 0.
    /// </summary>
    private static int GetSortRank(DirectorySizeEntry entry) {
      if (entry.TotalBytes > 0) {
        return 0;
      }
      if (entry.SymbolicLink != null) {
        return 1;
      }
      if (entry.Excluded && entry.IsDirectory && entry.MeasureState != MeasureState.Done) {
        return 2;
      }
      return 3;
    }

    /// <summary>
    /// The target of the symbolic link <paramref name="link"/>: from the map of the links (no disk
    /// access), otherwise resolved (the Windows file system reuses the resolution of the scan).
    /// <paramref name="targetTotals"/>: the size of the target (the file, or the files seen through
    /// the directory link).
    /// </summary>
    private SymbolicLinkInfo DescribeSymbolicLink(FileSystemSnapshot snapshot, FullPath link, bool isDirectory, EntryTotals targetTotals) {
      var info = new SymbolicLinkInfo {
        Kind = isDirectory ? SymbolicLinkKind.Directory : SymbolicLinkKind.File,
        Target = "",
      };
      var target = _snapshotManager.SymLinkTargets.GetTarget(link) ?? _fileSystem.ResolveSymLinkTarget(link, allowCached: true);
      if (target == null || !FullPath.IsValid(target)) {
        info.Dangling = true;
        info.Target = ReadLinkText(link, isDirectory);
        return info;
      }
      target = SymLinkTargetMap.Normalize(target);
      info.Target = target;
      info.TargetTotalBytes = targetTotals.TotalBytes;
      info.TargetFileCount = targetTotals.FileCount;
      var project = snapshot.ProjectRoots.FirstOrDefault(
        x => SymLinkTargetMap.IsSameOrUnder(target, SymLinkTargetMap.Normalize(x.Project.RootPath.Value)));
      if (project != null) {
        var root = SymLinkTargetMap.Normalize(project.Project.RootPath.Value);
        var relativePath = target.Substring(root.Length)
          .TrimStart(PathHelpers.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
          .Replace(System.IO.Path.DirectorySeparatorChar, '/');
        var slash = relativePath.LastIndexOf('/');
        var directory = isDirectory ? relativePath : (slash >= 0 ? relativePath.Substring(0, slash) : "");
        // Note: Only if the explorer can show it (e.g. not in an ignored directory).
        if (TryFindIndexedDirectory(project, directory) != null) {
          info.TargetProjectPath = project.Project.RootPath.Value;
          info.TargetRelativePath = relativePath;
        }
      } else if (SymLinkTargetMap.IsOnNetworkVolume(target)) {
        info.Network = true;
      } else {
        info.External = true;
      }
      return info;
    }

    /// <summary>The target written in a dangling link (full path), "" if unknown.</summary>
    private static string ReadLinkText(FullPath link, bool isDirectory) {
      try {
        System.IO.FileSystemInfo info = isDirectory
          ? new System.IO.DirectoryInfo(link.Value)
          : new System.IO.FileInfo(link.Value);
        var text = info.LinkTarget;
        if (string.IsNullOrEmpty(text)) {
          return "";
        }
        return System.IO.Path.GetFullPath(text, System.IO.Path.GetDirectoryName(link.Value) ?? link.Value);
      }
      catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException || e is ArgumentException ||
                                e is NotSupportedException) {
        return "";
      }
    }

    private static DirectorySnapshot TryFindIndexedDirectory(ProjectRootSnapshot project, string relativePath) {
      var directory = project.Directory;
      foreach (var part in PathHelpers.SplitPath(relativePath ?? "")) {
        directory = directory.ChildDirectories.FirstOrDefault(x => SystemPathComparer.EqualsNames(x.DirectoryName.Name, part));
        if (directory == null) {
          return null;
        }
      }
      return directory;
    }

    private static ProjectRootSnapshot FindProject(FileSystemSnapshot snapshot, string projectPath) {
      if (string.IsNullOrEmpty(projectPath) || !PathHelpers.IsAbsolutePath(projectPath)) {
        throw new RecoverableErrorException($"Invalid project path \"{projectPath}\"");
      }
      var path = new FullPath(projectPath);
      var project = snapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.Equals(path));
      if (project == null) {
        throw new RecoverableErrorException($"Project \"{projectPath}\" not found");
      }
      return project;
    }

    private static DirectorySnapshot FindIndexedDirectory(
      ProjectRootSnapshot project,
      string relativePath,
      out string normalizedRelativePath) {
      relativePath ??= "";
      var directory = project.Directory;
      var parts = new List<string>();
      foreach (var part in PathHelpers.SplitPath(relativePath)) {
        if (part == "." || part == "..") {
          throw new RecoverableErrorException($"Invalid relative path \"{relativePath}\"");
        }
        var next = directory.ChildDirectories.FirstOrDefault(
          x => SystemPathComparer.EqualsNames(x.DirectoryName.Name, part));
        if (next == null) {
          throw new RecoverableErrorException($"Directory \"{relativePath}\" not found in index");
        }
        directory = next;
        parts.Add(directory.DirectoryName.Name);
      }
      normalizedRelativePath = parts.Count == 0 ? "" : string.Join("/", parts);
      return directory;
    }

    private (FullPath FullPath, string NormalizedRelativePath) ResolveMeasurableDirectory(
      ProjectRootSnapshot project,
      string relativePath) {
      var (fullPath, normalizedRelPath) = ValidateRelativeSubPath(project.Project.RootPath, relativePath);

      var indexed = project.Directory;
      foreach (var part in PathHelpers.SplitPath(normalizedRelPath)) {
        indexed = indexed?.ChildDirectories.FirstOrDefault(
          x => SystemPathComparer.EqualsNames(x.DirectoryName.Name, part));
      }
      if (indexed != null) {
        throw new RecoverableErrorException($"Directory \"{normalizedRelPath}\" is already indexed.");
      }

      var info = _fileSystem.GetFileInfoSnapshot(fullPath);
      if (!info.Exists || !info.IsDirectory) {
        throw new RecoverableErrorException($"Directory \"{normalizedRelPath}\" does not exist.");
      }

      return (fullPath, normalizedRelPath);
    }

    private static (FullPath FullPath, string NormalizedRelativePath) ValidateRelativeSubPath(
      FullPath rootPath,
      string relativePath) {
      if (string.IsNullOrWhiteSpace(relativePath)) {
        throw new RecoverableErrorException("Relative directory path must not be empty.");
      }

      var parts = new List<string>();
      var rel = RelativePath.Empty;
      foreach (var part in PathHelpers.SplitPath(relativePath)) {
        if (string.IsNullOrEmpty(part) || part == "." || part == "..") {
          throw new RecoverableErrorException($"Invalid relative path \"{relativePath}\"");
        }
        parts.Add(part);
        rel = rel.CreateChild(part);
      }
      if (parts.Count == 0) {
        throw new RecoverableErrorException("Relative directory path must not be empty.");
      }

      var fullPath = rootPath.Combine(rel);
      if (!rootPath.ContainsPath(fullPath) || fullPath.Equals(rootPath)) {
        throw new RecoverableErrorException($"Invalid relative path \"{relativePath}\"");
      }

      return (fullPath, string.Join("/", parts));
    }

    private struct EntryTotals {
      public long TotalBytes;
      public long SearchableBytes;
      public long FileCount;
      public long SearchableFileCount;

      public void Add(EntryTotals other) {
        TotalBytes += other.TotalBytes;
        SearchableBytes += other.SearchableBytes;
        FileCount += other.FileCount;
        SearchableFileCount += other.SearchableFileCount;
      }
    }

    private sealed class CachedProjectSizes {
      public CachedProjectSizes(
        int snapshotVersion,
        DirectorySnapshot rootDirectory,
        FileDatabaseSnapshot database,
        Dictionary<DirectorySnapshot, EntryTotals> directoryTotals) {
        SnapshotVersion = snapshotVersion;
        RootDirectory = rootDirectory;
        Database = database;
        DirectoryTotals = directoryTotals;
      }

      public int SnapshotVersion { get; }
      public DirectorySnapshot RootDirectory { get; }
      public FileDatabaseSnapshot Database { get; }
      public Dictionary<DirectorySnapshot, EntryTotals> DirectoryTotals { get; }
    }

    private sealed class MeasurementJob {
      private readonly string _relativePath;
      private readonly CancellationTokenSource _cts = new CancellationTokenSource();
      private readonly Stopwatch _stopwatch = new Stopwatch();
      private readonly TaskCompletionSource _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      private readonly object _stateLock = new object();

      private MeasureState _state = MeasureState.Running;
      private long _totalBytes;
      private long _fileCount;
      private long _directoryCount;
      private string _error;

      public MeasurementJob(string relativePath) {
        _relativePath = relativePath;
      }

      public CancellationToken Token => _cts.Token;
      public Task Completion => _completion.Task;

      public MeasureState State {
        get {
          lock (_stateLock) {
            return _state;
          }
        }
      }

      public void Start(Action work) {
        _stopwatch.Start();
        _ = Task.Run(work);
      }

      public void AddFile(long bytes) {
        Interlocked.Increment(ref _fileCount);
        if (bytes > 0) {
          Interlocked.Add(ref _totalBytes, bytes);
        }
      }

      public void AddDirectory() {
        Interlocked.Increment(ref _directoryCount);
      }

      public void Complete() {
        lock (_stateLock) {
          if (_state != MeasureState.Running) {
            return;
          }
          _stopwatch.Stop();
          _state = MeasureState.Done;
        }
        _completion.TrySetResult();
      }

      public void Cancel() {
        lock (_stateLock) {
          if (_state == MeasureState.Running) {
            _stopwatch.Stop();
            _state = MeasureState.Cancelled;
          }
        }
        try {
          _cts.Cancel();
        }
        catch (ObjectDisposedException) {
        }
        _completion.TrySetResult();
      }

      public void MarkCancelled() {
        lock (_stateLock) {
          if (_state == MeasureState.Running) {
            _stopwatch.Stop();
            _state = MeasureState.Cancelled;
          }
        }
        _completion.TrySetResult();
      }

      public void Fail(string message) {
        lock (_stateLock) {
          if (_state != MeasureState.Running) {
            return;
          }
          _stopwatch.Stop();
          _error = message;
          _state = MeasureState.Failed;
        }
        _completion.TrySetResult();
      }

      public DirectoryMeasurement ToProtocol() {
        MeasureState state;
        string error;
        double elapsedMs;
        lock (_stateLock) {
          state = _state;
          error = _error;
          elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;
        }
        return new DirectoryMeasurement {
          RelativePath = _relativePath,
          State = state,
          TotalBytes = Interlocked.Read(ref _totalBytes),
          FileCount = Interlocked.Read(ref _fileCount),
          DirectoryCount = Interlocked.Read(ref _directoryCount),
          ElapsedMs = elapsedMs,
          Error = error,
        };
      }
    }
  }
}
