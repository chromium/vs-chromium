namespace VsChromium.Protocol;

model SearchQuery {
  /** The text to search, with "*" wildcards, or a regular expression (see "regex"). */
  text: string;
  /**
   * File path patterns limiting the search, separated by ";", e.g. "*.cc;*.h". Patterns starting
   * with "-" exclude files, e.g. "-*_test.cc".
   */
  filePathPattern?: string;
  matchCase?: boolean;
  matchWholeWord?: boolean;
  /** "text" is a regular expression (.NET syntax, non backtracking). */
  regex?: boolean;
  /** Also search files located in directories that are symbolic links. */
  includeSymLinks?: boolean;
}

model SearchParams {
  query: SearchQuery;
  /** Maximum number of matches (code search) or files (path search). */
  maxResults: int32;
}

/** The matches of a code search in a file. */
model FileMatches {
  /** Path of the file, relative to the project root. */
  relativePath: string;
  matches: TextSpan[];
}

/** The matches of a code search in the files of a project. */
model ProjectMatches {
  rootPath: string;
  files: FileMatches[];
}

model SearchCodeResult {
  projects: ProjectMatches[];
  /** Number of matches (at most "maxResults"). */
  hitCount: int32;
  /** Number of files searched (files matching "filePathPattern"). */
  searchedFileCount: int32;
  /** Number of searchable files in the index. */
  totalFileCount: int32;
}

/** The files of a project found by a path search. */
model ProjectFiles {
  rootPath: string;
  /** Paths of the files, relative to the project root. */
  relativePaths: string[];
}

model SearchPathsResult {
  projects: ProjectFiles[];
  /** Number of files found (at most "maxResults"). */
  hitCount: int32;
  /** Number of files searched. */
  totalCount: int32;
}

interface Search {
  /** Searches the contents of the indexed files. */
  code(...SearchParams): SearchCodeResult;

  /** Searches the paths of the indexed files ("text" is matched against names or relative paths). */
  paths(...SearchParams): SearchPathsResult;
}
