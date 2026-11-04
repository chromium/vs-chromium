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
  operationTimings: OperationTiming[];
}

interface Server {
  /** First request sent by a client: exchanges versions. */
  initialize(...InitializeParams): InitializeResult;

  /** Process, memory and index statistics. */
  statistics(...ServerStatisticsParams): ServerStatistics;
}
