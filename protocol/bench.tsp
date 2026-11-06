namespace VsChromium.Protocol;

/** The benchmark features of a server started with "--bench" (in the result of "server/initialize"). */
model BenchCapabilities {
  /** The "bench/*" requests supported by the server. */
  requests: string[];
}

/**
 * The result of "bench/saveCache": the save of the index cache started by the request (none if the
 * index didn't change since the last save).
 */
model BenchSaveCacheResult {
  /** False if there was nothing to save (the cache is up to date). */
  saved: boolean;
  /** Duration of the save (the last save if `saved` is false). */
  durationMs: float64;
  /** Bytes written by the save (new arena files and index). */
  bytes: int64;
  /** Files in the cache after the save. */
  entryCount: int32;
}

model BenchPhasesParams {
  /** The project of the "initialLoad" and "verification" values (the first project if absent). */
  projectPath?: string;
}

/** The last duration of a named operation of the server (e.g. "FileSystemScan", "FileDatabaseUpdate.Load"). */
model BenchPhase {
  name: string;
  lastMs: float64;
  count: int64;
  totalMs: float64;
}

/**
 * The phases of the last loads, for benchmarks: the last duration of each operation of the server
 * (scan, load of the files, index cache), the initial load and the verification of a project, and
 * the CPU time of the process.
 */
model BenchPhases {
  phases: BenchPhase[];
  /** The last initial load of the project (scan to complete index). */
  initialLoadMs: float64;
  initialLoadScanMs: float64;
  /** The time to the published index of the cache of the last initial load (0 if not from the cache). */
  cachedIndexMs: float64;
  /** The last verification of the tree of the index cache (0 if none). */
  verificationMs: float64;
  cpuUserMs: float64;
  cpuKernelMs: float64;
}

/**
 * Requests for benchmarks (vschromium-bench, src/Tools/Bench), only when the server runs with
 * "--bench" (otherwise "method not found"); listed in the `bench` capabilities of "server/initialize".
 */
interface Bench {
  /** Saves the index cache now (if it changed) and returns when the save is done. */
  saveCache(): BenchSaveCacheResult;

  /** The phases of the last loads. */
  phases(...BenchPhasesParams): BenchPhases;
}
