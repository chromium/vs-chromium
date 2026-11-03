namespace VsChromium.Protocol;

model TreeParams {
  /** Version of the tree known by the client: if it is the current version, "projects" is empty. */
  knownVersion?: int32;
}

model ProjectEntry {
  rootPath: string;
}

/** The projects known to the server (the file system tree, version numbered). */
model FileSystemTree {
  version: int32;
  projects: ProjectEntry[];
}

model TreeEntriesParams {
  /** Root path of the project. */
  projectPath: string;
  /** Path of the directory, relative to the project root ("" for the root). */
  relativePath: string;
}

model TreeEntriesMultipleParams {
  /** Root path of the project. */
  projectPath: string;
  /** Paths of the directories, relative to the project root. */
  relativePaths: string[];
}

/** The entries (sub directories and files) of a directory. */
model DirectoryEntries {
  /** Path of the directory, relative to the project root. */
  relativePath: string;
  /** False if the directory doesn't exist (or is not indexed): no entries. */
  exists: boolean;
  directoryNames: string[];
  fileNames: string[];
}

model DirectoryEntriesList {
  /** The entries of each requested directory, in the order of the request. */
  directories: DirectoryEntries[];
}

model DirectoryDetailsParams {
  /** Absolute path of the directory. */
  path: string;
  ...DetailsParams;
}

interface Tree {
  /** The projects known to the server. */
  get(...TreeParams): FileSystemTree;

  /** The entries of a directory of a project. */
  entries(...TreeEntriesParams): DirectoryEntries;

  /** The entries of several directories of a project. */
  entriesMultiple(...TreeEntriesMultipleParams): DirectoryEntriesList;

  /** Rescans the file system (e.g. after changes not reported by the file system watcher). */
  refresh(): void;

  /** Statistics of the files of a directory (with "files by extension" and "large files" lists). */
  directoryDetails(...DirectoryDetailsParams): DirectoryDetails;
}
