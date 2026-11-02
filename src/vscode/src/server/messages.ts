// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// TypeScript definitions of the JSON messages exchanged with VsChromium.Server.exe in "--stdio"
// mode. These mirror the C# classes in src/Core/Ipc (see IpcJsonSerializer.cs for the mapping):
// - property names are camelCase,
// - polymorphic values carry a "$type" property with the C# class name,
// - enums are strings.

export const IpcProtocols = {
  hello: 'hello',
  echo: 'echo',
  typedMessage: 'typed-message',
  exception: 'exception',
} as const;

// ---------------------------------------------------------------------------
// Envelope

export interface IpcMessage {
  $type: 'IpcRequest' | 'IpcResponse' | 'IpcEvent';
  requestId: number;
  protocol: string;
  data?: IpcMessageData;
}

export interface IpcRequest extends IpcMessage {
  $type: 'IpcRequest';
  runOnSequentialQueue?: boolean;
}

export interface IpcResponse extends IpcMessage {
  $type: 'IpcResponse' | 'IpcEvent';
}

export interface IpcEvent extends IpcResponse {
  $type: 'IpcEvent';
}

export type IpcMessageData = IpcStringData | ErrorResponse | TypedMessage;

export interface IpcStringData {
  $type: 'IpcStringData';
  text?: string;
}

export interface ErrorResponse {
  $type: 'ErrorResponse';
  message?: string;
  fullTypeName?: string;
  stackTrace?: string;
  innerError?: ErrorResponse;
}

export interface TypedMessage {
  $type: string;
  /** Set by the server on all typed messages (C# class name). Optional when sending. */
  className?: string;
}

// ---------------------------------------------------------------------------
// Requests

export interface RegisterFileRequest extends TypedMessage {
  $type: 'RegisterFileRequest';
  fileName: string;
}

export interface UnregisterFileRequest extends TypedMessage {
  $type: 'UnregisterFileRequest';
  fileName: string;
}

export interface GetDatabaseStatisticsRequest extends TypedMessage {
  $type: 'GetDatabaseStatisticsRequest';
  forceGabageCollection?: boolean;
}

export interface SearchParams {
  searchString: string;
  /** Optional file path pattern(s), e.g. "*.cc;*.h" or "-*_test.cc" to exclude. */
  filePathPattern?: string;
  maxResults: number;
  matchCase?: boolean;
  matchWholeWord?: boolean;
  includeSymLinks?: boolean;
  regex?: boolean;
  useRe2Engine?: boolean;
}

export interface SearchCodeRequest extends TypedMessage {
  $type: 'SearchCodeRequest';
  searchParams: SearchParams;
}

export interface FilePositionSpan {
  /** Offset (in UTF-16 characters) of the first character of the span. */
  position: number;
  length: number;
}

export interface GetFileExtractsRequest extends TypedMessage {
  $type: 'GetFileExtractsRequest';
  fileName: string;
  positions: FilePositionSpan[];
  maxExtractLength: number;
}

export type TypedRequest =
  | RegisterFileRequest
  | UnregisterFileRequest
  | GetDatabaseStatisticsRequest
  | SearchCodeRequest
  | GetFileExtractsRequest;

// ---------------------------------------------------------------------------
// Responses

export interface DoneResponse extends TypedMessage {
  $type: 'DoneResponse';
  info?: string;
}

export type IndexingServerStatus = 'Idle' | 'Paused' | 'Yield' | 'Busy';

export interface GetDatabaseStatisticsResponse extends TypedMessage {
  $type: 'GetDatabaseStatisticsResponse';
  projectCount: number;
  fileCount: number;
  searchableFileCount: number;
  serverNativeMemoryUsage: number;
  serverGcMemoryUsage: number;
  indexLastUpdatedUtc: string;
  serverStatus: IndexingServerStatus;
}

/** Maps each request type to its response type. */
export interface ResponseTypes {
  RegisterFileRequest: DoneResponse;
  UnregisterFileRequest: DoneResponse;
  GetDatabaseStatisticsRequest: GetDatabaseStatisticsResponse;
  SearchCodeRequest: SearchCodeResponse;
  GetFileExtractsRequest: GetFileExtractsResponse;
}

export interface FilePositionsData {
  $type: 'FilePositionsData';
  positions: FilePositionSpan[];
}

export interface FileEntry {
  $type: 'FileEntry';
  /** Path relative to the parent directory entry. */
  name: string;
  data?: FilePositionsData;
}

export interface DirectoryEntry {
  /** Omitted when the declared type is DirectoryEntry (e.g. SearchCodeResponse.searchResults). */
  $type?: 'DirectoryEntry';
  name: string;
  entries?: (FileEntry | DirectoryEntry)[];
}

export interface SearchCodeResponse extends TypedMessage {
  $type: 'SearchCodeResponse';
  /**
   * Root entry (empty name) containing one directory entry per project (name = project root
   * path), each containing the matching files (name = path relative to the project root).
   */
  searchResults: DirectoryEntry;
  hitCount: number;
  searchedFileCount: number;
  totalFileCount: number;
}

export interface FileExtract {
  /** Text of the line containing the match (possibly truncated to maxExtractLength). */
  text: string;
  /** Offset of `text` in the file. */
  offset: number;
  length: number;
  /** 0-based line number. */
  lineNumber: number;
  /** 0-based column of the match in the line. */
  columnNumber: number;
}

export interface GetFileExtractsResponse extends TypedMessage {
  $type: 'GetFileExtractsResponse';
  fileName: string;
  /** One extract per requested position (may contain nulls for positions that are out of range). */
  fileExtracts: (FileExtract | null)[];
}

// ---------------------------------------------------------------------------
// Events (sent by the server at any time)

export interface ProgressReportEvent extends TypedMessage {
  $type: 'ProgressReportEvent';
  displayText?: string;
  completed: number;
  total: number;
}

export interface IndexingServerStateChangedEvent extends TypedMessage {
  $type: 'IndexingServerStateChangedEvent';
  serverStatus: IndexingServerStatus;
  lastIndexUpdatedUtc: string;
}

/** Events reporting the start/end of a long running operation, correlated by operationId. */
export interface PairedTypedEvent extends TypedMessage {
  $type:
    | 'FileSystemScanStarted'
    | 'FileSystemScanFinished'
    | 'SearchEngineFilesLoading'
    | 'SearchEngineFilesLoadingProgress'
    | 'SearchEngineFilesLoaded';
  operationId: number;
  error?: ErrorResponse;
}

export interface FileSystemScanFinished extends PairedTypedEvent {
  $type: 'FileSystemScanFinished';
  oldVersion: number;
  newVersion: number;
}

export type TypedEvent = ProgressReportEvent | IndexingServerStateChangedEvent | PairedTypedEvent;
