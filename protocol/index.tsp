namespace VsChromium.Protocol;

/** State of the indexing of the server. */
enum IndexingStatus {
  /** The index is up to date. */
  idle,
  /** Indexing is paused (see "index/pause"). */
  paused,
  /** The server paused itself (file system watcher buffer overflow): call "index/resume". */
  yield,
  /** The server is indexing the file system. */
  busy,
  /**
   * The index of the last session (index cache) is in use, and is being verified in the background
   * (scan and incremental update): search results may contain deleted files or miss new or large
   * files until it ends.
   */
  verifying,
}

model IndexStatisticsParams {
  /** Perform a full garbage collection before collecting the statistics. */
  forceGarbageCollection?: boolean;
}

model IndexStatistics {
  projectCount: int32;
  fileCount: int32;
  searchableFileCount: int32;
  /** Native memory allocated by the server (file contents: arenas and blocks of large files). */
  serverNativeMemoryUsage: int64;
  serverGcMemoryUsage: int64;
  /** Bytes of the text of the indexed files. */
  indexedTextBytes: int64;
  fileContentsArenas: FileContentsArenaStatistics;
  indexLastUpdatedUtc: utcDateTime;
  status: IndexingStatus;
  /** The persistent index cache (absent if the server has none). */
  indexCache?: IndexCacheState;
}

/**
 * The state of the index for frequent status refreshes (e.g. every 2 s while a dashboard is
 * visible): values kept by the server only, no walk of the index and no disk access (a few
 * microseconds). `index/statistics` returns the same values plus the arena statistics
 * (`fileContentsArenas`: a walk of the file contents, tens of milliseconds on large indexes).
 */
model IndexStatus {
  status: IndexingStatus;
  indexLastUpdatedUtc: utcDateTime;
  projectCount: int32;
  fileCount: int32;
  searchableFileCount: int32;
  /** Bytes of the text of the indexed files. */
  indexedTextBytes: int64;
  /** Native memory allocated by the server (file contents: arenas and blocks of large files). */
  serverNativeMemoryUsage: int64;
  /** Managed memory of the server (GC heap). */
  serverGcMemoryUsage: int64;
  /** The persistent index cache (absent if the server has none). */
  indexCache?: IndexCacheState;
}

model ClearCacheParams {
  /**
   * The root path of the project whose cache is deleted (`rootPath` of `tree/get`). All the
   * projects of the index if absent.
   */
  projectPath?: string;
}

model ClearCacheResult {
  /** Projects whose cache was deleted (0 if the cache is disabled). */
  projectCount: int32;
}

/** The state of the persistent index cache, for status displays. */
model IndexCacheState {
  enabled: boolean;
  /** A save of the cache is running. */
  saving: boolean;
  /** Last write of the index of the caches of the open projects (the most recent), if any. */
  lastSavedUtc?: utcDateTime;
  /**
   * Size of the indexes of the caches of the open projects and of their arena files, as of their last
   * read or write (no disk access).
   */
  sizeOnDiskBytes: int64;
  /** Arena files of the indexes of the open project caches (the text of the files). */
  arenaFileCount: int32;
  /** Bytes used in these arena files. */
  arenaBytes: int64;
  /**
   * Bytes of these arena files not used by the index anymore (files modified or deleted since
   * they were saved): removed by the compaction during the saves.
   */
  deadBytes: int64;
  /**
   * The index changed since the last save (started) of the caches written by this server: saved
   * once the changes settle (30 s without change, at most 5 minutes after the first unsaved change).
   * False if the cache is disabled, or if the caches of all the open projects are written by another
   * server (read-only).
   */
  dirty: boolean;
}

model IndexDetails {
  projects: ProjectDetails[];
}

model IndexStateChangedParams {
  status: IndexingStatus;
  lastIndexUpdatedUtc: utcDateTime;
}

/** Notification of the start of a long operation (scan, load). */
model OperationStartedParams {
  /** Identifier of the operation (same in the notification of the end of the operation). */
  operationId: int64;
}

model ScanFinishedParams {
  operationId: int64;
  oldVersion: int32;
  newVersion: int32;
  /** Set if the scan failed. */
  error?: ErrorInfo;
}

model FilesLoadedParams {
  operationId: int64;
  /** Version of the file system tree that was loaded. */
  treeVersion: int64;
  /** Set if the load failed. */
  error?: ErrorInfo;
  /**
   * True if the loaded index comes from the index cache (tree and contents of the last session)
   * and is being verified (status "verifying"): another "loaded" notification follows when the
   * verification completes.
   */
  fromCache?: boolean;
}

model ProgressParams {
  displayText: string;
  completed: int32;
  total: int32;
}

interface Index {
  /** Pauses indexing: file system changes are not processed until "index/resume". */
  pause(): void;

  /** Resumes indexing (after "index/pause", or when the server paused itself). */
  resume(): void;

  /** Statistics of the index (including the arena statistics: a walk of the file contents). */
  statistics(...IndexStatisticsParams): IndexStatistics;

  /** State of the index without the arena statistics: cheap, for frequent refreshes. */
  status(): IndexStatus;

  /**
   * Deletes the persistent index cache of a project (or of all the projects): the same deletion
   * as "tree/refresh" (Rebuild index), without the rescan. The current index stays in use (the
   * contents loaded from the cache stay readable). The cache is written again by the save policy:
   * the index is marked as changed, saved once it settles (30 s), or after the load in progress,
   * if any. A server stopped before that save starts cold the next time.
   */
  clearCache(...ClearCacheParams): ClearCacheResult;

  /** Details of all the projects. */
  details(...DetailsParams): IndexDetails;
}

/** Notifications sent by the server. */
interface IndexNotifications {
  /** The indexing state changed. */
  stateChanged(...IndexStateChangedParams): void;

  /** A file system scan started. */
  scanStarted(...OperationStartedParams): void;

  /** A file system scan finished. */
  scanFinished(...ScanFinishedParams): void;

  /** Loading of file contents started. */
  loading(...OperationStartedParams): void;

  /** Progress of the loading of file contents (sent periodically during the loading). */
  loadingProgress(...OperationStartedParams): void;

  /** Loading of file contents finished: the index is up to date. */
  loaded(...FilesLoadedParams): void;

  /** Progress of a long operation, for display. */
  progress(...ProgressParams): void;
}
