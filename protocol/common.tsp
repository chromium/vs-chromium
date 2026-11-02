namespace VsChromium.Protocol;

/** A span of text in a file: offset and length in UTF-16 code units. */
model TextSpan {
  /** Offset of the first character of the span, from the beginning of the file. */
  position: int32;
  length: int32;
}

/** Description of an error that happened in the server (e.g. a failed file system scan). */
model ErrorInfo {
  message: string;
  /** Full name of the .NET exception type, if any. */
  typeName?: string;
  stackTrace?: string;
  /** The error that caused this error, if any. */
  inner?: ErrorInfo;
}
