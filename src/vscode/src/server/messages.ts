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

export type TypedRequest =
  | RegisterFileRequest
  | UnregisterFileRequest
  | GetDatabaseStatisticsRequest;

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
