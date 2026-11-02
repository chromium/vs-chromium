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
