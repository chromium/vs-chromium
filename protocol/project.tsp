namespace VsChromium.Protocol;

model SetProjectOptionsParams {
  /** Root path of the project, or any path inside the project. */
  projectPath: string;

  /**
   * Enables or disables the trigram filters of the project (disabled by default): faster code
   * searches, at the cost of memory (about 5% of the size of the file contents). When disabled,
   * the filters of the project are released. Unchanged if omitted.
   */
  trigramFiltersEnabled?: boolean;
}

model DetailsParams {
  /** Maximum number of entries of the "files by extension" lists. */
  maxFilesByExtensionDetailsCount: int32;
  /** Maximum number of entries of the "large files" lists. */
  maxLargeFilesDetailsCount: int32;
}

model ProjectDetailsParams {
  /** Root path of the project. */
  projectPath: string;
  ...DetailsParams;
}

model FileByExtensionDetails {
  fileExtension: string;
  fileCount: int32;
  filesByteLength: int64;
}

model LargeFileDetails {
  relativePath: string;
  byteLength: int64;
}

/** Statistics of the files of a directory (recursively). */
model DirectoryDetails {
  path: string;
  directoryCount: int32;
  fileCount: int32;
  searchableFilesCount: int32;
  searchableFilesByteLength: int64;
  binaryFilesCount: int32;
  binaryFilesByteLength: int64;
  searchableFilesByExtensionDetails: FileByExtensionDetails[];
  largeSearchableFilesDetails: LargeFileDetails[];
  binaryFilesByExtensionDetails: FileByExtensionDetails[];
  largeBinaryFilesDetails: LargeFileDetails[];
}

/** A section of a project configuration file (e.g. "SearchableFiles.ignore"). */
model ProjectConfigurationSection {
  /** Path of the file containing the section. */
  containingFilePath: string;
  name: string;
  contents: string;
}

model ProjectConfiguration {
  ignorePathsSection: ProjectConfigurationSection;
  ignoreSearchableFilesSection: ProjectConfigurationSection;
  includeSearchableFilesSection: ProjectConfigurationSection;
}

/**
 * The memory of a project. The file contents of each project are allocated in its own arenas (16 MB
 * blocks shared by the files of the project up to 512 KB), released when the project is removed
 * and its contents are not used anymore. The sums over the projects are the `fileContentsArenas`
 * of `index/statistics`.
 */
model ProjectMemory {
  /** Native memory of the file contents of the project: `arenaBytes` + `largeFileBytes`. */
  nativeBytes: int64;
  arenaCount: int32;
  /** Native memory of the arenas of the project. */
  arenaBytes: int64;
  /** Bytes of the arenas used by the files of the current index. */
  liveBytes: int64;
  /** Bytes of the arenas used by files edited or deleted since they were read (released with their arena). */
  deadBytes: int64;
  /** Bytes of the arenas never allocated: the end of the current arena and of the full arenas. */
  freeBytes: int64;
  /** Bytes of the files larger than 512 KB (a dedicated block per file, outside the arenas). */
  largeFileBytes: int64;
  /** Bytes of the files loaded from the index cache of the project (memory mapped files, not native memory). */
  mappedCacheBytes: int64;
  /** Memory used by the trigram filters of the project (managed memory). */
  trigramFiltersBytes: int64;
}

/** The state of the persistent index cache of a project (from the state of the server, no disk access). */
model ProjectCacheState {
  /** True if the project has a cache (the cache of the server is enabled and could be opened for the project). */
  enabled: boolean;
  /** The directory of the cache of the project (`<cache root>\<hash of the root path>`), if any. */
  directory?: string;
  /** False if the cache of the project is written by another server (used read-only). */
  writable: boolean;
  /** Size of the index of the cache and of its arena files, as of their last read or write. */
  sizeOnDiskBytes: int64;
  /** Bytes of the arena files of the cache not used anymore (removed by the compaction of the saves). */
  deadBytes: int64;
  /** Last save of the cache of the project, if any. */
  lastSavedUtc?: utcDateTime;
  /** The index of the project changed since the last save (started) of its cache: saved by the save policy. */
  dirty: boolean;
  /** The cache of the project is being written. */
  saving: boolean;
}

model ProjectDetails {
  rootPath: string;
  directoryDetails: DirectoryDetails;
  configuration: ProjectConfiguration;
  /** True if the trigram filters of the project are enabled (see "project/setOptions"). */
  trigramFiltersEnabled: boolean;
  /** Memory used by the trigram filters of the project. */
  trigramFiltersBytes: int64;
  memory: ProjectMemory;
  cache: ProjectCacheState;
}

interface Project {
  /**
   * Changes the options of a project, at any time (also before the project is loaded). Options are
   * kept for the lifetime of the server process: clients send them again after starting a server.
   */
  setOptions(...SetProjectOptionsParams): void;

  /** Details of a project: statistics of its files and configuration. */
  details(...ProjectDetailsParams): ProjectDetails;
}
