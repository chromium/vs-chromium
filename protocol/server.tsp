namespace VsChromium.Protocol;

model InitializeParams {
  /** Name of the client, e.g. "Visual Studio" or "VS Code" (for logging). */
  clientName?: string;
  clientVersion?: string;
  /** Version of the protocol implemented by the client. */
  protocolVersion: int32;
}

model InitializeResult {
  serverVersion: string;
  /** Version of the protocol implemented by the server. */
  protocolVersion: int32;
  serverProcessId: int32;
  /** Only when the server runs with "--bench" (benchmarks). */
  bench?: BenchCapabilities;
}

model ServerStatisticsParams {
  /** Perform a full garbage collection before collecting the statistics. */
  forceGarbageCollection?: boolean;
}

/** Duration statistics of a named operation of the server (e.g. "FileSystemScan"). */
model OperationTiming {
  name: string;
  count: int64;
  lastMs: float64;
  totalMs: float64;
  maxMs: float64;
  /** The last durations (at most 20), oldest first. */
  recentMs: float64[];
  /** The end times of `recentMs`, in milliseconds since the Unix epoch (UTC). */
  recentEndUnixMs: int64[];
}

/**
 * Native memory of the arenas storing the text of the indexed files (16 MB blocks shared by the
 * files up to 512 KB). Dead bytes are slots of files edited or deleted since they were allocated:
 * an arena is released when none of its slots is in use anymore.
 */
model FileContentsArenaStatistics {
  arenaCount: int32;
  arenaBytes: int64;
  /**
   * Bytes allocated in the arenas. The rest is free: the end of the current arena, and the ends of
   * the full arenas (too small for the file that didn't fit).
   */
  usedBytes: int64;
  /** Bytes of the files of the current index. */
  liveBytes: int64;
  /** Number of full arenas with less than half of their allocated bytes in use. */
  sparseArenaCount: int32;
  /** Bytes of the files larger than 512 KB (a dedicated block per file, outside the arenas). */
  largeFileBytes: int64;
  /**
   * Bytes of the files loaded from the index cache: slots of the arena files of the cache, memory
   * mapped (pages of files, not native memory, not counted in the arenas above).
   */
  cachedFileBytes: int64;
}

/**
 * The persistent index cache: the contents of the files are written to a cache directory after a
 * full load, and used by the next loads (e.g. after a restart) for the files whose last write time
 * and size are unchanged.
 */
model IndexCacheStatistics {
  enabled: boolean;
  /** The root directory of the cache (one sub-directory per project). */
  cacheDirectory?: string;
  /** Projects whose cache is open. */
  projectCount: int32;
  /** Projects whose cache is used read-only (written by another server). */
  readOnlyProjectCount: int32;
  /** Files of the last full load whose contents came from the cache. */
  lastLoadFilesFromCache: int32;
  /** Files of the last full load read from disk. */
  lastLoadFilesFromDisk: int32;
  /** Completed writes of the cache. */
  writeCount: int32;
  lastWriteMs: float64;
  /** Bytes written by the last write (new arena files and index). */
  lastWriteBytes: int64;
  /** Files in the cache after the last write. */
  lastWriteEntryCount: int32;
  /** Files whose contents were written to new arena files by the last write. */
  lastWriteNewFileCount: int32;
  /** Arena files compacted by the last write (live slots copied to new arena files). */
  lastWriteCompactedArenaCount?: int32;
  /** Bytes copied by the compaction of the last write. */
  lastWriteCompactedBytes?: int64;
  /** Change journal checkpoints saved alone while the index didn't change (idle advance, every 30 minutes). */
  checkpointUpdateCount?: int32;
  /**
   * Files not written by the last write because their time in the scan is not the time of their
   * contents: modified after the scan, or (on Windows) stale directory entries of hard links and
   * files being written, file symbolic links. Read from disk at each load.
   */
  lastWriteChangedFileCount: int32;
}

/** Process, memory and index statistics of the server (diagnostics and benchmarks). */
model ServerStatistics {
  runtimeDescription: string;
  processId: int32;
  processorCount: int32;
  uptimeMs: float64;
  cpuTotalMs: float64;
  cpuUserMs: float64;
  cpuKernelMs: float64;
  workingSetBytes: int64;
  peakWorkingSetBytes: int64;
  privateBytes: int64;
  threadCount: int32;
  handleCount: int32;
  gcHeapBytes: int64;
  gcTotalAllocatedBytes: int64;
  gcGen0Collections: int32;
  gcGen1Collections: int32;
  gcGen2Collections: int32;
  gcTotalPauseMs: float64;
  /** Native memory allocated by the server (mostly file contents). */
  nativeAllocatedBytes: int64;
  indexedFileContentsBytes: int64;
  trigramFiltersBytes: int64;
  fileContentsArenas: FileContentsArenaStatistics;
  fileCount: int32;
  searchableFileCount: int32;
  projectCount: int32;
  indexCache?: IndexCacheStatistics;
  operationTimings: OperationTiming[];
}

model ServerHistoryParams {
  /**
   * Only the samples taken after this time (milliseconds since the Unix epoch, UTC), e.g. the time
   * of the last sample already received (incremental refresh). All the samples if absent.
   */
  sinceUnixMs?: int64;
}

/**
 * The history of the server: a sample every `intervalMs` (10 s), the last `capacity` samples (720: 2
 * hours), kept by the server from its start (also when no client asks for them). The samples are
 * returned oldest first, one array per metric (columns): the values of the sample `i` are
 * `timesUnixMs[i]`, `gcHeapBytes[i]`, ... (all the arrays have the same length).
 *
 * Cheap metrics only: counters and values already known by the server (no walk of the index, no
 * disk access). The mapped bytes of the index cache are not sampled (see the `cachedFileBytes` of
 * the arena statistics of `index/statistics`).
 */
model ServerHistory {
  /** The interval of the samples, in milliseconds. */
  intervalMs: int32;
  /** The maximum number of samples kept by the server (the oldest ones are dropped). */
  capacity: int32;
  /** Time of the samples, in milliseconds since the Unix epoch (UTC). */
  timesUnixMs: int64[];
  /** Managed memory (`server/statistics` `gcHeapBytes`). */
  gcHeapBytes: int64[];
  /** Total managed memory allocated since the start (`gcTotalAllocatedBytes`): the slope is the allocation rate. */
  gcTotalAllocatedBytes: int64[];
  /** Native memory allocated by the server (`nativeAllocatedBytes`): mostly file contents. */
  nativeAllocatedBytes: int64[];
  workingSetBytes: int64[];
  privateBytes: int64[];
  /** CPU time of the process since its start (user and kernel): the slope is the CPU usage. */
  cpuTotalMs: float64[];
  /** Files of the current index. */
  fileCount: int32[];
  /** Searchable files of the current index (files with text contents). */
  searchableFileCount: int32[];
  /** Bytes of the text of the indexed files (`index/statistics` `indexedTextBytes`). */
  indexedTextBytes: int64[];
  /** Size of the index cache of the open projects (`indexCache.sizeOnDiskBytes`), as of its last read or write. */
  cacheSizeOnDiskBytes: int64[];
  /** State of the indexing. */
  status: IndexingStatus[];
}

interface Server {
  /** First request sent by a client: exchanges versions. */
  initialize(...InitializeParams): InitializeResult;

  /** Process, memory and index statistics. */
  statistics(...ServerStatisticsParams): ServerStatistics;

  /** History of the server (charts): cheap samples every 10 s, last 2 hours. */
  history(...ServerHistoryParams): ServerHistory;
}
