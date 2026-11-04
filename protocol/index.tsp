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

/** The state of the persistent index cache, for status displays. */
model IndexCacheState {
  enabled: boolean;
  /** A write of the cache is queued or running. */
  saving: boolean;
  /** Last write of the index of the caches of the open projects (the most recent), if any. */
  lastSavedUtc?: utcDateTime;
  /** Size of the cache directories of the open projects. */
  sizeOnDiskBytes: int64;
  /**
   * The index changed after the last write of the caches written by this server (e.g. edited
   * files: only full loads write the cache). False if the cache is disabled, or if the caches of
   * all the open projects are written by another server (read-only).
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

  /** Statistics of the index. */
  statistics(...IndexStatisticsParams): IndexStatistics;

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
