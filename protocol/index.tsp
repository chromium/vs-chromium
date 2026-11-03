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
}

model IndexStatisticsParams {
  /** Perform a full garbage collection before collecting the statistics. */
  forceGarbageCollection?: boolean;
}

model IndexStatistics {
  projectCount: int32;
  fileCount: int32;
  searchableFileCount: int32;
  serverNativeMemoryUsage: int64;
  serverGcMemoryUsage: int64;
  fileContentsArenas: FileContentsArenaStatistics;
  indexLastUpdatedUtc: utcDateTime;
  status: IndexingStatus;
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
