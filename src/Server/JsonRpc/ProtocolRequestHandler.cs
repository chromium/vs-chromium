// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Memory;
using VsChromium.Core.Utility;
using Microsoft.Win32.SafeHandles;
using VsChromium.Protocol.Generated;
using VsChromium.Server.Cache;
using VsChromium.Server.Diagnostics;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;
using ISearchEngine = VsChromium.Server.Search.ISearchEngine;
using EngineFilePositionSpan = VsChromium.Server.Search.FilePositionSpan;
using EngineSearchParams = VsChromium.Server.Search.SearchParams;
using OperationTiming = VsChromium.Protocol.Generated.OperationTiming;

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// The requests of the protocol (JSON-RPC), implemented with the services of the server (ported
  /// from the request handlers of the typed messages protocol, removed in P5).
  ///
  /// The services are synchronous: the methods complete synchronously, on the thread pool thread
  /// of the request (see <see cref="JsonRpcConnection"/>).
  /// </summary>
  public sealed class ProtocolRequestHandler : IProtocolRequestHandler {
    /// <summary>The version of the protocol implemented by the server ("server/initialize").</summary>
    public const int ProtocolVersion = 1;

    private readonly IFileSystemSnapshotManager _snapshotManager;
    private readonly ISearchEngine _searchEngine;
    private readonly IFileRegistrationTracker _fileRegistrationTracker;
    private readonly IIndexingServer _indexingServer;
    private readonly ProjectArenaAllocators _arenaAllocators;
    private readonly IIndexCache _indexCache;
    private readonly ServerHistorySampler _historySampler;
    private readonly ProjectDiskUsageService _diskUsage;

    public ProtocolRequestHandler(
      IFileSystemSnapshotManager snapshotManager,
      ISearchEngine searchEngine,
      IFileRegistrationTracker fileRegistrationTracker,
      IIndexingServer indexingServer,
      ProjectArenaAllocators arenaAllocators,
      IIndexCache indexCache = null,
      ServerHistorySampler historySampler = null,
      ProjectDiskUsageService diskUsage = null) {
      _diskUsage = diskUsage;
      _indexCache = indexCache;
      _historySampler = historySampler;
      _snapshotManager = snapshotManager;
      _searchEngine = searchEngine;
      _fileRegistrationTracker = fileRegistrationTracker;
      _indexingServer = indexingServer;
      _arenaAllocators = arenaAllocators;
    }

    // server/*

    public ValueTask<InitializeResult> ServerInitializeAsync(InitializeParams parameters, CancellationToken cancellationToken) {
      Logger.LogInfo("Client \"{0}\" version \"{1}\" (protocol version {2}) connected.",
        parameters.ClientName, parameters.ClientVersion, parameters.ProtocolVersion);
      if (parameters.ProtocolVersion != ProtocolVersion) {
        Logger.LogWarn("The client protocol version ({0}) is not the server protocol version ({1}).",
          parameters.ProtocolVersion, ProtocolVersion);
      }
      return new ValueTask<InitializeResult>(new InitializeResult {
        ServerVersion = VsChromiumVersion.Product,
        ProtocolVersion = ProtocolVersion,
        ServerProcessId = Environment.ProcessId,
      });
    }

    public ValueTask<ServerStatistics> ServerStatisticsAsync(ServerStatisticsParams parameters, CancellationToken cancellationToken) {
      if (parameters.ForceGarbageCollection == true) {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
      }

      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      var snapshot = _snapshotManager.CurrentSnapshot;
      using (var process = System.Diagnostics.Process.GetCurrentProcess()) {
        return new ValueTask<ServerStatistics>(new ServerStatistics {
          RuntimeDescription = RuntimeInformation.FrameworkDescription + " " + RuntimeInformation.ProcessArchitecture +
                               " (" + RuntimeInformation.OSDescription + ")",
          ProcessId = process.Id,
          ProcessorCount = Environment.ProcessorCount,
          UptimeMs = (DateTime.Now - process.StartTime).TotalMilliseconds,
          CpuTotalMs = process.TotalProcessorTime.TotalMilliseconds,
          CpuUserMs = process.UserProcessorTime.TotalMilliseconds,
          CpuKernelMs = process.PrivilegedProcessorTime.TotalMilliseconds,
          WorkingSetBytes = process.WorkingSet64,
          PeakWorkingSetBytes = process.PeakWorkingSet64,
          PrivateBytes = process.PrivateMemorySize64,
          ThreadCount = process.Threads.Count,
          HandleCount = process.HandleCount,

          GcHeapBytes = GC.GetTotalMemory(false),
          GcTotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false),
          GcGen0Collections = GC.CollectionCount(0),
          GcGen1Collections = GC.CollectionCount(1),
          GcGen2Collections = GC.CollectionCount(2),
          GcTotalPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds,

          NativeAllocatedBytes = NativeHeap.TotalMemory,
          IndexedFileContentsBytes = database.TotalFileContentsLength,
          TrigramFiltersBytes = GetTrigramFiltersBytes(database),
          FileContentsArenas = GetArenaStatistics(database),
          FileCount = ToInt32(database.FileNameCount),
          SearchableFileCount = ToInt32(database.SearchableFileCount),
          ProjectCount = snapshot.ProjectRoots.Count,
          IndexCache = GetIndexCacheStatistics(),

          OperationTimings = OperationTimings.GetSnapshot()
            .Select(x => new OperationTiming {
              Name = x.Name,
              Count = x.Count,
              LastMs = x.LastMs,
              TotalMs = x.TotalMs,
              MaxMs = x.MaxMs,
              RecentMs = x.RecentMs.ToList(),
              RecentEndUnixMs = x.RecentEndUnixMs.ToList(),
            })
            .ToList(),
        });
      }
    }

    public ValueTask<ServerHistory> ServerHistoryAsync(ServerHistoryParams parameters, CancellationToken cancellationToken) {
      var buffer = _historySampler?.History;
      var samples = buffer?.GetSamples(parameters.SinceUnixMs) ?? new ServerHistoryBuffer(1).GetSamples();
      return new ValueTask<ServerHistory>(new ServerHistory {
        IntervalMs = (int)(_historySampler?.Interval ?? ServerHistorySampler.DefaultInterval).TotalMilliseconds,
        Capacity = buffer?.Capacity ?? ServerHistoryBuffer.DefaultCapacity,
        TimesUnixMs = samples.TimeUnixMs.ToList(),
        GcHeapBytes = samples.GcHeapBytes.ToList(),
        GcTotalAllocatedBytes = samples.GcTotalAllocatedBytes.ToList(),
        NativeAllocatedBytes = samples.NativeAllocatedBytes.ToList(),
        WorkingSetBytes = samples.WorkingSetBytes.ToList(),
        PrivateBytes = samples.PrivateBytes.ToList(),
        CpuTotalMs = samples.CpuTotalMs.ToList(),
        FileCount = samples.FileCount.ToList(),
        SearchableFileCount = samples.SearchableFileCount.ToList(),
        IndexedTextBytes = samples.IndexedTextBytes.ToList(),
        CacheSizeOnDiskBytes = samples.CacheSizeOnDiskBytes.ToList(),
        Status = samples.Status.Select(JsonRpcEventForwarder.ToIndexingStatus).ToList(),
      });
    }

    private static long GetTrigramFiltersBytes(IFileDatabaseSnapshot database) {
      long total = 0;
      foreach (var piece in database.FileContentsPieces) {
        if (piece.IsFirstPiece)
          total += piece.FileContents.TrigramFiltersByteSize;
      }
      return total;
    }

    /// <summary>The memory of the file contents of all the projects (the sum of the projects).</summary>
    private FileContentsArenaStatistics GetArenaStatistics(IFileDatabaseSnapshot database) {
      var statistics = FileContentsStatistics.Sum(FileContentsStatistics.GetProjectStatistics(_arenaAllocators, database).Values);
      return new FileContentsArenaStatistics {
        ArenaCount = statistics.ArenaCount,
        ArenaBytes = statistics.ArenaBytes,
        UsedBytes = statistics.UsedBytes,
        LiveBytes = statistics.LiveBytes,
        SparseArenaCount = statistics.SparseArenaCount,
        LargeFileBytes = statistics.OtherBytes,
        CachedFileBytes = statistics.CachedBytes,
      };
    }

    private Protocol.Generated.IndexCacheStatistics GetIndexCacheStatistics() {
      if (_indexCache == null) {
        return null;
      }
      var statistics = _indexCache.GetStatistics();
      return new Protocol.Generated.IndexCacheStatistics {
        Enabled = statistics.Enabled,
        CacheDirectory = statistics.CacheDirectory,
        ProjectCount = statistics.ProjectCount,
        ReadOnlyProjectCount = statistics.ReadOnlyProjectCount,
        LastLoadFilesFromCache = statistics.LastLoadFilesFromCache,
        LastLoadFilesFromDisk = statistics.LastLoadFilesFromDisk,
        WriteCount = statistics.WriteCount,
        LastWriteMs = statistics.LastWriteMs,
        LastWriteBytes = statistics.LastWriteBytes,
        LastWriteEntryCount = statistics.LastWriteEntryCount,
        LastWriteNewFileCount = statistics.LastWriteNewFileCount,
        LastWriteChangedFileCount = statistics.LastWriteChangedFileCount,
        LastWriteCompactedArenaCount = statistics.LastWriteCompactedArenaCount,
        LastWriteCompactedBytes = statistics.LastWriteCompactedBytes,
      };
    }

    /// <summary>
    /// The arenas of <paramref name="arenaAllocator"/> and the bytes used by the file contents of
    /// <paramref name="database"/>.
    /// <para>
    /// Note: Counted from the contents array, not from the pieces: the intermediate snapshots of a
    /// load share the contents array of the load in progress (so they see the contents loaded
    /// since they were created, which are not dead), and empty files have a slot but no piece.
    /// </para>
    /// <para>
    /// The contents in the memory mapped arena files of the index cache are counted separately
    /// (<see cref="ArenaStatistics.CachedBytes"/>): they are not native heap memory.
    /// </para>
    /// </summary>
    public static ArenaStatistics GetArenaStatistics(ArenaAllocator arenaAllocator, IFileDatabaseSnapshot database) {
      var contents = database is FileDatabaseSnapshot snapshot
        ? snapshot.ContentsArray
        : database.FileContentsPieces.Where(x => x.IsFirstPiece).Select(x => x.FileContents);
      return FileContentsStatistics.GetArenaStatistics(arenaAllocator, contents);
    }

    // files/*

    public ValueTask FilesRegisterAsync(FilesRegisterParams parameters, CancellationToken cancellationToken) {
      _fileRegistrationTracker.RegisterFileAsync(new FullPath(parameters.Path));
      return default;
    }

    public ValueTask FilesUnregisterAsync(FilesUnregisterParams parameters, CancellationToken cancellationToken) {
      _fileRegistrationTracker.UnregisterFileAsync(new FullPath(parameters.Path));
      return default;
    }

    public ValueTask<FileExtractsResult> FilesExtractsAsync(FileExtractsParams parameters, CancellationToken cancellationToken) {
      var spans = parameters.Spans.Select(x => new EngineFilePositionSpan { Position = x.Position, Length = x.Length });
      var extracts = _searchEngine.GetFileExtracts(new FullPath(parameters.Path), spans, parameters.MaxExtractLength);
      return new ValueTask<FileExtractsResult>(new FileExtractsResult {
        Path = parameters.Path,
        Extracts = extracts
          .Select(x => new FileExtract {
            Text = x.Text ?? "",
            Offset = x.Offset,
            Length = x.Length,
            LineNumber = x.LineNumber,
            ColumnNumber = x.ColumnNumber,
          })
          .ToList(),
      });
    }

    // index/*

    public ValueTask IndexPauseAsync(CancellationToken cancellationToken) {
      _indexingServer.Pause();
      return default;
    }

    public ValueTask IndexResumeAsync(CancellationToken cancellationToken) {
      _indexingServer.Resume();
      return default;
    }

    public ValueTask<IndexStatistics> IndexStatisticsAsync(IndexStatisticsParams parameters, CancellationToken cancellationToken) {
      if (parameters.ForceGarbageCollection == true) {
        using (new TimeElapsedLogger("Forcing a full garbage collection before returning statistics")) {
          GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }
      }

      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      var indexingServerState = _indexingServer.CurrentState;
      return new ValueTask<IndexStatistics>(new IndexStatistics {
        ProjectCount = snapshot.ProjectRoots.Count,
        FileCount = ToInt32(database.FileNameCount),
        SearchableFileCount = ToInt32(database.SearchableFileCount),
        ServerNativeMemoryUsage = NativeHeap.TotalMemory,
        IndexLastUpdatedUtc = indexingServerState.LastIndexUpdateUtc,
        ServerGcMemoryUsage = GC.GetTotalMemory(false),
        IndexedTextBytes = database.TotalFileContentsLength,
        FileContentsArenas = GetArenaStatistics(database),
        Status = JsonRpcEventForwarder.ToIndexingStatus(indexingServerState.Status),
        IndexCache = GetIndexCacheState(),
      });
    }

    public ValueTask<IndexStatus> IndexStatusAsync(CancellationToken cancellationToken) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      var indexingServerState = _indexingServer.CurrentState;
      // Note: The counts of the database are computed when it is activated (PrecomputeSearchData).
      return new ValueTask<IndexStatus>(new IndexStatus {
        Status = JsonRpcEventForwarder.ToIndexingStatus(indexingServerState.Status),
        IndexLastUpdatedUtc = indexingServerState.LastIndexUpdateUtc,
        ProjectCount = snapshot.ProjectRoots.Count,
        FileCount = ToInt32(database.FileNameCount),
        SearchableFileCount = ToInt32(database.SearchableFileCount),
        IndexedTextBytes = database.TotalFileContentsLength,
        ServerNativeMemoryUsage = NativeHeap.TotalMemory,
        ServerGcMemoryUsage = GC.GetTotalMemory(false),
        IndexCache = GetIndexCacheState(),
      });
    }

    public ValueTask<ClearCacheResult> IndexClearCacheAsync(CancellationToken cancellationToken) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      return new ValueTask<ClearCacheResult>(ClearCache(snapshot, snapshot.ProjectRoots.Select(x => x.Project.RootPath).ToList()));
    }

    public ValueTask<ClearCacheResult> ProjectClearCacheAsync(ProjectPathParams parameters, CancellationToken cancellationToken) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      return new ValueTask<ClearCacheResult>(ClearCache(snapshot, new List<FullPath> { FindProjectRoot(snapshot, parameters.ProjectPath) }));
    }

    public ValueTask ProjectRebuildAsync(ProjectPathParams parameters, CancellationToken cancellationToken) {
      var root = FindProjectRoot(_snapshotManager.CurrentSnapshot, parameters.ProjectPath);
      Logger.LogInfo("Rebuilding the index of the project \"{0}\".", root);
      _indexingServer.RebuildProject(root);
      return default;
    }

    public ValueTask<DirectorySizes> ProjectDirectorySizesAsync(DirectoryPathParams parameters, CancellationToken cancellationToken) {
      EnsureDiskUsageService();
      return new ValueTask<DirectorySizes>(_diskUsage.GetDirectorySizes(parameters));
    }

    public ValueTask<DirectoryMeasurement> ProjectMeasureDirectoryAsync(MeasureDirectoryParams parameters, CancellationToken cancellationToken) {
      EnsureDiskUsageService();
      return _diskUsage.MeasureDirectoryAsync(parameters, cancellationToken);
    }

    public ValueTask<DirectoryMeasurement> ProjectCancelMeasureAsync(DirectoryPathParams parameters, CancellationToken cancellationToken) {
      EnsureDiskUsageService();
      return new ValueTask<DirectoryMeasurement>(_diskUsage.CancelMeasure(parameters));
    }

    public ValueTask<AddIgnorePatternResult> ProjectAddIgnorePatternAsync(AddIgnorePatternParams parameters, CancellationToken cancellationToken) {
      EnsureDiskUsageService();
      return new ValueTask<AddIgnorePatternResult>(_diskUsage.AddIgnorePattern(parameters));
    }

    private void EnsureDiskUsageService() {
      if (_diskUsage == null) {
        throw new RecoverableErrorException("Disk usage service is not available.");
      }
    }

    /// <summary>The root path of a project of <paramref name="snapshot"/> (-32001 if not found).</summary>
    private static FullPath FindProjectRoot(FileSystemSnapshot snapshot, string projectPath) {
      if (string.IsNullOrEmpty(projectPath) || !PathHelpers.IsAbsolutePath(projectPath)) {
        throw new RecoverableErrorException($"Invalid project path \"{projectPath}\"");
      }
      var path = new FullPath(projectPath);
      var project = snapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.Equals(path));
      if (project == null) {
        throw new RecoverableErrorException($"Project \"{projectPath}\" not found");
      }
      return project.Project.RootPath;
    }

    private ClearCacheResult ClearCache(FileSystemSnapshot snapshot, List<FullPath> roots) {
      if (_indexCache == null || !_indexCache.IsEnabled) {
        return new ClearCacheResult { ProjectCount = 0 };
      }
      Logger.LogInfo("Clearing the index cache of {0:n0} project(s).", roots.Count);
      _indexCache.Invalidate(roots);
      SaveIndexCacheLater(snapshot);
      return new ClearCacheResult { ProjectCount = roots.Count };
    }

    /// <summary>
    /// The current index is saved to the cache by the save policy (the index is "changed"). If a
    /// load is in progress (or the index is the tree of the cache, being verified), it is saved
    /// after it.
    /// </summary>
    private void SaveIndexCacheLater(FileSystemSnapshot snapshot) {
      var database = snapshot.IsFromCache ? null : _searchEngine.GetCompleteDatabase(snapshot.Version);
      if (database != null) {
        _indexCache.NotifyIndexUpdated(snapshot, database, fullLoad: false);
      }
    }
    /// <summary>
    /// The state of the index cache for status displays (no walk of the index, no disk access).
    /// </summary>
    private IndexCacheState GetIndexCacheState() {
      if (_indexCache == null) {
        return null;
      }
      var state = _indexCache.GetDiskState();
      return new IndexCacheState {
        Enabled = state.Enabled,
        Saving = state.Saving,
        LastSavedUtc = state.LastSavedUtc,
        SizeOnDiskBytes = state.SizeOnDiskBytes,
        ArenaFileCount = state.ArenaFileCount,
        ArenaBytes = state.ArenaBytes,
        DeadBytes = state.DeadBytes,
        // Note: The index changed since the last save (false if the cache is disabled, or if the
        // caches of all the open projects are read-only: written by another server).
        Dirty = state.Dirty,
      };
    }

    public ValueTask<IndexDetails> IndexDetailsAsync(DetailsParams parameters, CancellationToken cancellationToken) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      return new ValueTask<IndexDetails>(new IndexDetails {
        Projects = snapshot.ProjectRoots
          .Select(project => CreateProjectDetails(database, project, parameters.MaxFilesByExtensionDetailsCount,
            parameters.MaxLargeFilesDetailsCount))
          .ToList(),
      });
    }

    // project/*

    public ValueTask ProjectSetOptionsAsync(SetProjectOptionsParams parameters, CancellationToken cancellationToken) {
      if (string.IsNullOrEmpty(parameters.ProjectPath) || !PathHelpers.IsAbsolutePath(parameters.ProjectPath)) {
        throw new RecoverableErrorException($"Invalid project path \"{parameters.ProjectPath}\"");
      }
      _searchEngine.SetProjectOptions(new FullPath(parameters.ProjectPath), parameters.TrigramFiltersEnabled);
      if (parameters.CacheEnabled.HasValue && _indexCache != null) {
        // Note: The path may be any path inside the project (or a project not loaded yet).
        var root = _searchEngine.GetProjectRoot(new FullPath(parameters.ProjectPath));
        if (_indexCache.SetProjectCacheEnabled(root, parameters.CacheEnabled.Value) && parameters.CacheEnabled.Value) {
          SaveIndexCacheLater(_snapshotManager.CurrentSnapshot);
        }
      }
      return default;
    }

    public ValueTask<ProjectDetails> ProjectDetailsAsync(ProjectDetailsParams parameters, CancellationToken cancellationToken) {
      var projectPath = new FullPath(parameters.ProjectPath);
      var projectSnapshot = _snapshotManager.CurrentSnapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.Equals(projectPath));
      if (projectSnapshot == null) {
        throw new RecoverableErrorException($"Project \"{parameters.ProjectPath}\" not found");
      }

      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      return new ValueTask<ProjectDetails>(CreateProjectDetails(database, projectSnapshot,
        parameters.MaxFilesByExtensionDetailsCount, parameters.MaxLargeFilesDetailsCount));
    }

    private ProjectDetails CreateProjectDetails(IFileDatabaseSnapshot database, ProjectRootSnapshot project,
      int maxFilesByExtensionDetailsCount, int maxLargeFilesDetailsCount) {
      return new ProjectDetails {
        RootPath = project.Project.RootPath.Value,
        DirectoryDetails = CreateDirectoryDetails(database, project.Directory, maxFilesByExtensionDetailsCount,
          maxLargeFilesDetailsCount),
        Configuration = new ProjectConfiguration {
          IgnorePathsSection = CreateSection(project.Project.IgnorePathsConfiguration),
          IgnoreSearchableFilesSection = CreateSection(project.Project.IgnoreSearchableFilesConfiguration),
          IncludeSearchableFilesSection = CreateSection(project.Project.IncludeSearchableFilesConfiguration),
        },
        TrigramFiltersEnabled = _searchEngine.IsTrigramFiltersEnabled(project.Project.RootPath),
        TrigramFiltersBytes = _searchEngine.GetTrigramFiltersBytes(project.Project.RootPath),
        Memory = CreateProjectMemory(database, project.Project.RootPath),
        Cache = CreateProjectCacheState(project.Project.RootPath),
        Operations = CreateProjectOperations(project.Project.RootPath),
      };
    }

    private static ProjectOperationsStatistics CreateProjectOperations(FullPath rootPath) {
      var statistics = ProjectOperations.GetStatistics(rootPath);
      var load = statistics.InitialLoad;
      var updates = statistics.IncrementalUpdates;
      return new ProjectOperationsStatistics {
        InitialLoad = new InitialLoadStatistics {
          Count = load.Count, MinMs = load.MinMs, AvgMs = load.AverageMs, MaxMs = load.MaxMs, LastMs = load.LastMs,
          ScanMs = statistics.LastScanMs,
          FromCache = statistics.LastFromCache,
          CachedIndexMs = statistics.LastFromCache ? statistics.LastCachedIndexMs : null,
        },
        IncrementalUpdates = new DurationStatistics {
          Count = updates.Count, MinMs = updates.MinMs, AvgMs = updates.AverageMs, MaxMs = updates.MaxMs, LastMs = updates.LastMs,
        },
        LastVerification = CreateCacheVerification(statistics.LastVerification),
      };
    }

    private static CacheVerificationStatistics CreateCacheVerification(ProjectVerification verification) {
      if (verification == null)
        return null;
      return new CacheVerificationStatistics {
        Method = verification.Method == FileSystem.VerificationMethod.ChangeJournal
          ? Protocol.Generated.VerificationMethod.ChangeJournal
          : Protocol.Generated.VerificationMethod.FullScan,
        FallbackReason = verification.FallbackReason,
        DurationMs = verification.ElapsedMs,
        JournalRecordCount = verification.RecordCount,
        ListedDirectoryCount = verification.ListedDirectoryCount,
        ScannedDirectoryCount = verification.ScannedSubtreeCount,
        SymLinkDirectoryCount = verification.SymLinkSubtreeCount,
        SymLinkFileCount = verification.SymLinkFileCount,
      };
    }

    private ProjectMemory CreateProjectMemory(IFileDatabaseSnapshot database, FullPath rootPath) {
      var statistics = FileContentsStatistics.GetProjectStatistics(_arenaAllocators, database).GetValueOrDefault(rootPath);
      return new ProjectMemory {
        NativeBytes = statistics.ArenaBytes + statistics.OtherBytes,
        ArenaCount = statistics.ArenaCount,
        ArenaBytes = statistics.ArenaBytes,
        LiveBytes = statistics.LiveBytes,
        DeadBytes = statistics.DeadBytes,
        FreeBytes = statistics.ArenaBytes - statistics.UsedBytes,
        LargeFileBytes = statistics.OtherBytes,
        MappedCacheBytes = statistics.CachedBytes,
        TrigramFiltersBytes = _searchEngine.GetTrigramFiltersBytes(rootPath),
      };
    }

    private ProjectCacheState CreateProjectCacheState(FullPath rootPath) {
      var state = _indexCache?.GetProjectState(rootPath) ?? new IndexCacheProjectState();
      return new ProjectCacheState {
        Enabled = state.Enabled,
        Directory = state.Directory,
        Writable = state.IsWritable,
        SizeOnDiskBytes = state.SizeOnDiskBytes,
        DeadBytes = state.DeadBytes,
        LastSavedUtc = state.LastSavedUtc,
        Dirty = state.Dirty,
        Saving = state.Saving,
      };
    }

    private static ProjectConfigurationSection CreateSection(IConfigurationSectionContents section) {
      return new ProjectConfigurationSection {
        ContainingFilePath = section.ContainingFilePath.Value,
        Name = section.Name,
        Contents = section.Contents.Aggregate("", (acc, s1) => acc + "\r\n" + s1),
      };
    }

    // search/*

    public ValueTask<SearchCodeResult> SearchCodeAsync(SearchParams parameters, CancellationToken cancellationToken) {
      var result = _searchEngine.SearchCode(ToSearchParams(parameters), cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      return new ValueTask<SearchCodeResult>(new SearchCodeResult {
        Projects = result.Entries
          .GroupBy(x => GetProjectRoot(x.FileName))
          .OrderBy(g => g.Key)
          .Select(g => new ProjectMatches {
            RootPath = g.Key.FullPath.Value,
            Files = g
              .OrderBy(x => x.FileName.RelativePath)
              .Select(x => new FileMatches {
                RelativePath = x.FileName.RelativePath.Value,
                Matches = x.Spans.Select(span => new TextSpan { Position = span.Position, Length = span.Length }).ToList(),
              })
              .ToList(),
          })
          .ToList(),
        HitCount = ToInt32(result.HitCount),
        SearchedFileCount = ToInt32(result.SearchedFileCount),
        TotalFileCount = ToInt32(result.TotalFileCount),
      });
    }

    public ValueTask<SearchPathsResult> SearchPathsAsync(SearchParams parameters, CancellationToken cancellationToken) {
      var result = _searchEngine.SearchFilePaths(ToSearchParams(parameters), cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      return new ValueTask<SearchPathsResult>(new SearchPathsResult {
        Projects = result.FileNames
          .GroupBy(GetProjectRoot)
          .OrderBy(g => g.Key)
          .Select(g => new ProjectFiles {
            RootPath = g.Key.FullPath.Value,
            RelativePaths = g
              .OrderBy(x => x.RelativePath)
              .Select(x => x.RelativePath.Value)
              .ToList(),
          })
          .ToList(),
        HitCount = result.FileNames.Count,
        TotalCount = ToInt32(result.TotalCount),
      });
    }

    private static EngineSearchParams ToSearchParams(SearchParams parameters) {
      var query = parameters.Query ?? throw new ProtocolInvalidParamsException("Missing \"query\"");
      return new EngineSearchParams {
        SearchString = query.Text,
        FilePathPattern = query.FilePathPattern,
        MaxResults = parameters.MaxResults,
        MatchCase = query.MatchCase ?? false,
        MatchWholeWord = query.MatchWholeWord ?? false,
        IncludeSymLinks = query.IncludeSymLinks ?? false,
        Regex = query.Regex ?? false,
      };
    }

    /// <summary>The root directory of the project of a file (the absolute directory of its name).</summary>
    private static DirectoryName GetProjectRoot(FileName name) {
      var directory = name.Parent;
      while (!directory.IsAbsoluteName) {
        directory = directory.Parent;
      }
      return directory;
    }

    private static int ToInt32(long value) {
      return (int)Math.Min(value, int.MaxValue);
    }

    // tree/*

    public ValueTask<FileSystemTree> TreeGetAsync(TreeParams parameters, CancellationToken cancellationToken) {
      var snapshot = _snapshotManager.CurrentSnapshot;
      var unchanged = parameters.KnownVersion == snapshot.Version;
      return new ValueTask<FileSystemTree>(new FileSystemTree {
        Version = snapshot.Version,
        Projects = unchanged
          ? new List<ProjectEntry>()
          : snapshot.ProjectRoots.Select(x => new ProjectEntry { RootPath = x.Project.RootPath.Value }).ToList(),
      });
    }

    public ValueTask<DirectoryEntries> TreeEntriesAsync(TreeEntriesParams parameters, CancellationToken cancellationToken) {
      var project = FindProject(parameters.ProjectPath);
      return new ValueTask<DirectoryEntries>(CreateDirectoryEntries(project, parameters.RelativePath));
    }

    public ValueTask<DirectoryEntriesList> TreeEntriesMultipleAsync(TreeEntriesMultipleParams parameters, CancellationToken cancellationToken) {
      var project = FindProject(parameters.ProjectPath);
      return new ValueTask<DirectoryEntriesList>(new DirectoryEntriesList {
        Directories = parameters.RelativePaths.Select(x => CreateDirectoryEntries(project, x)).ToList(),
      });
    }

    private ProjectRootSnapshot FindProject(string projectPath) {
      var path = new FullPath(projectPath);
      return _snapshotManager.CurrentSnapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.Equals(path));
    }

    private static DirectoryEntries CreateDirectoryEntries(ProjectRootSnapshot project, string relativePath) {
      relativePath ??= "";
      var directory = FindDirectory(project, relativePath);
      if (directory == null) {
        return new DirectoryEntries { RelativePath = relativePath, Exists = false };
      }
      return new DirectoryEntries {
        RelativePath = relativePath,
        Exists = true,
        DirectoryNames = directory.ChildDirectories.Select(x => x.DirectoryName.Name).ToList(),
        FileNames = directory.ChildFiles.Select(x => x.Name).ToList(),
      };
    }

    private static DirectorySnapshot FindDirectory(ProjectRootSnapshot project, string relativePath) {
      if (project == null) {
        return null;
      }
      var directory = project.Directory;
      foreach (var name in PathHelpers.SplitPath(relativePath)) {
        directory = directory.ChildDirectories.FirstOrDefault(x => SystemPathComparer.EqualsNames(x.DirectoryName.Name, name));
        if (directory == null) {
          return null;
        }
      }
      return directory;
    }

    public ValueTask TreeRefreshAsync(CancellationToken cancellationToken) {
      _indexingServer.Refresh();
      return default;
    }

    public ValueTask<DirectoryDetails> TreeDirectoryDetailsAsync(DirectoryDetailsParams parameters, CancellationToken cancellationToken) {
      var directoryPath = new FullPath(parameters.Path);
      var projectSnapshot = _snapshotManager.CurrentSnapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.ContainsPath(directoryPath));
      if (projectSnapshot == null) {
        throw new RecoverableErrorException($"Directory \"{parameters.Path}\" not found in index");
      }
      var splitPath = PathHelpers.SplitPrefix(directoryPath.Value, projectSnapshot.Project.RootPath.Value);
      var directorySnapshot = FindDirectory(projectSnapshot, splitPath.Suffix);
      if (directorySnapshot == null) {
        throw new RecoverableErrorException($"Directory \"{parameters.Path}\" not found in index");
      }

      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      return new ValueTask<DirectoryDetails>(CreateDirectoryDetails(database, directorySnapshot,
        parameters.MaxFilesByExtensionDetailsCount, parameters.MaxLargeFilesDetailsCount));
    }

    private static DirectoryDetails CreateDirectoryDetails(IFileDatabaseSnapshot database, DirectorySnapshot baseDirectory,
      int maxFilesByExtensionDetailsCount, int maxLargeFilesDetailsCount) {
      var directoryPath = baseDirectory.DirectoryName.FullPath;
      var fileDatabase = (FileDatabaseSnapshot)database;

      var directoryFiles = new List<FileWithContents>();
      var fileNames = fileDatabase.FileNameArray;
      for (var i = 0; i < fileNames.Length; i++) {
        if (directoryPath.ContainsPath(fileNames[i].FullPath)) {
          directoryFiles.Add(new FileWithContents(fileNames[i], fileDatabase.GetContents(i)));
        }
      }

      var searchableFiles = directoryFiles
        .Where(x => x.HasContents())
        .ToList();

      var binaryFiles = directoryFiles
        .Where(x => (x.Contents is BinaryFileContents) && (((BinaryFileContents)x.Contents).BinaryFileSize > 0))
        .ToList();

      return new DirectoryDetails {
        Path = directoryPath.Value,

        DirectoryCount = FileSystemSnapshotManager.CountDirectoryEntries(baseDirectory),

        FileCount = FileSystemSnapshotManager.CountFileEntries(baseDirectory),

        SearchableFilesCount = searchableFiles.Count,

        SearchableFilesByteLength = searchableFiles.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength),

        BinaryFilesCount = binaryFiles.Count,

        BinaryFilesByteLength = binaryFiles.Aggregate(0L, (acc, x) => acc + ((BinaryFileContents)x.Contents).BinaryFileSize),

        SearchableFilesByExtensionDetails = searchableFiles
          .GroupBy(x => GetFileExtension(x.FileName))
          .Select(g => new FileByExtensionDetails {
            FileExtension = g.Key,
            FileCount = g.Count(),
            FilesByteLength = g.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength)
          })
          .OrderByDescendingThenTake(maxFilesByExtensionDetailsCount, x => x.FilesByteLength)
          .ToList(),

        LargeSearchableFilesDetails = searchableFiles
          .OrderByDescendingThenTake(maxLargeFilesDetailsCount, x => x.Contents.ByteLength)
          .Select(x => new LargeFileDetails {
            RelativePath = GetRelativePath(directoryPath, x.FileName.FullPath),
            ByteLength = x.Contents.ByteLength
          })
          .ToList(),

        BinaryFilesByExtensionDetails = binaryFiles
          .GroupBy(x => GetFileExtension(x.FileName))
          .Select(g => new FileByExtensionDetails {
            FileExtension = g.Key,
            FileCount = g.Count(),
            FilesByteLength = g.Aggregate(0L, (acc, x) => acc + ((BinaryFileContents)x.Contents).BinaryFileSize)
          })
          .OrderByDescendingThenTake(maxFilesByExtensionDetailsCount, x => x.FilesByteLength)
          .ToList(),

        LargeBinaryFilesDetails = binaryFiles
          .OrderByDescendingThenTake(maxLargeFilesDetailsCount, x => ((BinaryFileContents)x.Contents).BinaryFileSize)
          .Select(x => new LargeFileDetails {
            RelativePath = GetRelativePath(directoryPath, x.FileName.FullPath),
            ByteLength = ((BinaryFileContents)x.Contents).BinaryFileSize
          })
          .ToList()
      };
    }

    private static string GetRelativePath(FullPath parentPath, FullPath path) {
      return PathHelpers.SplitPrefix(path.Value, parentPath.Value).Suffix;
    }

    private static string GetFileExtension(FileName fileName) {
      var ext = PathHelpers.GetExtension(fileName.Name);
      if (string.IsNullOrEmpty(ext)) {
        return fileName.Name;
      }
      return ext;
    }
  }
}
