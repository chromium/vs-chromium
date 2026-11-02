namespace VsChromium.Protocol;

model FileExtractsParams {
  /** Absolute path of the file. */
  path: string;
  /** Spans of text to extract (e.g. matches of a code search). */
  spans: TextSpan[];
  /** Maximum length of each extract (the text around each span, on the same line). */
  maxExtractLength: int32;
}

/** Text around a span of a file, on the same line. */
model FileExtract {
  text: string;
  /** Offset (UTF-16 code units) of the extract in the file. */
  offset: int32;
  length: int32;
  /** Line number (0 based) of the span. */
  lineNumber: int32;
  /** Column number (0 based) of the span. */
  columnNumber: int32;
}

model FileExtractsResult {
  path: string;
  /** The extracts, in the order of the requested spans. */
  extracts: FileExtract[];
}

interface Files {
  /**
   * Registers a file opened by the client: the project containing the file is discovered (e.g. a
   * directory with a vs-chromium-project.txt file) and indexed.
   */
  register(path: string): void;

  /** Unregisters a file registered with "files/register" (e.g. the file was closed). */
  unregister(path: string): void;

  /** Extracts of the text of a file around spans of text. */
  extracts(...FileExtractsParams): FileExtractsResult;
}
