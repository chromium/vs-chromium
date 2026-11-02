namespace VsChromium.Protocol;

model SetProjectOptionsParams {
  /** Root path of the project, or any path inside the project. */
  projectPath: string;

  /**
   * Enables or disables the trigram filters of the project (enabled by default): faster code
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

model ProjectDetails {
  rootPath: string;
  directoryDetails: DirectoryDetails;
  configuration: ProjectConfiguration;
  /** True if the trigram filters of the project are enabled (see "project/setOptions"). */
  trigramFiltersEnabled: boolean;
  /** Memory used by the trigram filters of the project. */
  trigramFiltersBytes: int64;
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
