// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import { spawn } from 'child_process';
import { EventEmitter } from 'events';
import * as fs from 'fs';
import * as readline from 'readline';
import { Readable, Writable } from 'stream';
import {
  CancellationToken,
  MessageConnection,
  NotificationType,
  RequestType,
  RequestType0,
  ResponseError,
  Trace,
} from 'vscode-jsonrpc';
import { createMessageConnection, StreamMessageReader, StreamMessageWriter } from 'vscode-jsonrpc/node';
import { InitializeResult, ServerInitializeRequest } from '../protocol/protocol.g';

/** Version of the protocol implemented by this client (see "server/initialize"). */
export const ProtocolVersion = 1;

/** JSON-RPC error codes of the server (see protocol/README.md, "Transport"). */
export const ErrorCodes = {
  /** Internal error of the server (unexpected exception). */
  internalError: -32603,
  /** Expected error (e.g. invalid regular expression): the message is shown to the user as is. */
  recoverableError: -32001,
  /** The request was cancelled ($/cancelRequest, or a newer search replaced it). */
  requestCancelled: -32800,
} as const;

/** Error returned by the server in response to a request (JSON-RPC error). */
export class ServerError extends Error {
  constructor(readonly code: number, message: string, readonly data?: unknown) {
    super(message);
    this.name = 'ServerError';
  }

  /** True if the request was cancelled: not an error to report. */
  get isCancelled(): boolean {
    return this.code === ErrorCodes.requestCancelled;
  }
}

/** The server process exited (or could not be started) before a request completed. */
export class ServerExitedError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ServerExitedError';
  }
}

/** True if `e` is the error of a cancelled request (to ignore). */
export function isCancelledError(e: unknown): boolean {
  return e instanceof ServerError && e.isCancelled;
}

/**
 * The message to show to the user for an error of a request, or undefined if the error must not
 * be reported (cancelled request).
 */
export function userErrorMessage(e: unknown): string | undefined {
  if (e instanceof ServerError) {
    switch (e.code) {
      case ErrorCodes.requestCancelled:
        return undefined;
      case ErrorCodes.recoverableError:
        return e.message;
      case ErrorCodes.internalError:
        return `Internal error of the indexing server: ${e.message}`;
      default:
        return `${e.message} (error ${e.code})`;
    }
  }
  return e instanceof Error ? e.message : String(e);
}

/**
 * The server process (a `ChildProcess`, or a fake in tests): emits "exit" (code, signal) and
 * "error" (Error).
 */
export interface ServerProcess extends EventEmitter {
  readonly stdin: Writable;
  readonly stdout: Readable;
  readonly stderr?: Readable | null;
  readonly pid?: number;
  kill(): void;
}

export interface ServerClientOptions {
  /** Called with diagnostic messages (server stderr, protocol errors, ...). */
  log?: (message: string) => void;
  /** Called with every message sent/received, for tracing. */
  trace?: (message: string) => void;
  /** Sent in "server/initialize" (default: "VS Code"). */
  clientName?: string;
  /** Sent in "server/initialize" (the version of the extension). */
  clientVersion?: string;
}

type Listener<T> = (value: T) => void;

interface PendingRequest {
  reject: (error: Error) => void;
}

/**
 * Client for VsChromium.Server.exe running in "--jsonrpc" mode: JSON-RPC 2.0 with LSP framing
 * on stdin/stdout (vscode-jsonrpc), typed with the generated protocol (protocol/protocol.g.ts).
 * Sends "server/initialize" first, dispatches server notifications to any number of listeners
 * and fails pending requests with `ServerExitedError` when the server exits.
 *
 * This class has no dependency on the VS Code API so it can be tested with plain Node.
 */
export class ServerClient {
  private readonly connection: MessageConnection;
  private readonly pending = new Set<PendingRequest>();
  private readonly notificationHandlers = new Map<string, Set<Listener<unknown>>>();
  private readonly exitListeners: Listener<string>[] = [];
  private readonly initializePromise: Promise<InitializeResult>;
  private exitReason: string | undefined;
  private rejectStartup: ((error: Error) => void) | undefined;

  /** Starts the server executable (`--jsonrpc`) and connects to it. */
  static start(serverPath: string, options: ServerClientOptions = {}): ServerClient {
    ensureExecutable(serverPath);
    const process = spawn(serverPath, ['--jsonrpc'], { windowsHide: true, stdio: 'pipe' });
    // Nothing is written before the process is started: writing to the stdin of a process that
    // failed to start makes vscode-jsonrpc raise an unhandled promise rejection.
    const spawned = new Promise<void>(resolve => process.once('spawn', () => resolve()));
    return new ServerClient(process, options, `"${serverPath}"`, spawned);
  }

  /**
   * Connects to a server process (see `start`). `spawned`: resolved when the process is started,
   * "server/initialize" is sent after it.
   */
  constructor(
    private readonly process: ServerProcess,
    private readonly options: ServerClientOptions = {},
    description = 'server',
    spawned: Promise<void> = Promise.resolve(),
  ) {
    const logger = {
      error: (m: string) => this.log(m),
      warn: (m: string) => this.log(m),
      info: (m: string) => this.log(m),
      log: (m: string) => this.log(m),
    };
    this.connection = createMessageConnection(
      new StreamMessageReader(process.stdout), new StreamMessageWriter(process.stdin), logger);
    // A single "star" handler: vscode-jsonrpc supports only one handler per method.
    this.connection.onNotification((method, params) => this.dispatchNotification(method, params));
    this.connection.onError(([error]) => this.log(`Protocol error: ${error.message}`));
    if (options.trace) {
      const trace = options.trace;
      this.connection.trace(Trace.Verbose, {
        log: (message: unknown, data?: string) => trace(data ? `${String(message)}\n${data}` : String(message)),
      });
    }

    if (process.stderr) {
      readline.createInterface({ input: process.stderr, crlfDelay: Infinity }).on('line', line => {
        this.log(`[server stderr] ${line}`);
      });
    }
    process.on('error', (error: Error) => this.handleExit(`Failed to start server ${description}: ${error.message}`));
    process.on('exit', (code: number | null, signal: string | null) =>
      this.handleExit(`Server process exited (code=${code ?? 'none'}, signal=${signal ?? 'none'})`));
    // Writing to stdin after the process exited raises EPIPE errors: these are handled by onExit.
    process.stdin.on('error', error => this.log(`Error writing to server: ${error.message}`));

    this.connection.listen();
    const startup = new Promise<void>((resolve, reject) => {
      this.rejectStartup = reject;
      void spawned.then(resolve);
    });
    this.initializePromise = startup.then(() => this.send(() => this.connection.sendRequest(ServerInitializeRequest.type, {
      clientName: options.clientName ?? 'VS Code',
      clientVersion: options.clientVersion,
      protocolVersion: ProtocolVersion,
    }))).then(result => {
      if (result.protocolVersion !== ProtocolVersion) {
        throw new Error(`The indexing server implements version ${result.protocolVersion} of the protocol ` +
          `(expected version ${ProtocolVersion}): the server and the extension are not from the same build.`);
      }
      return result;
    });
    // Avoid unhandled rejection warnings if nobody waits for "ready".
    this.initializePromise.catch(() => undefined);
  }

  /** Process id of the server, if started. */
  get pid(): number | undefined {
    return this.process.pid;
  }

  /** True once the server process has exited (or failed to start). */
  get hasExited(): boolean {
    return this.exitReason !== undefined;
  }

  /** Resolves when the server has answered "server/initialize" (sent when the client is created). */
  ready(): Promise<InitializeResult> {
    return this.initializePromise;
  }

  /**
   * Sends a request (after "server/initialize") and returns its result. Rejects with
   * `ServerError` (see `userErrorMessage`, `isCancelledError`) or `ServerExitedError`.
   * Cancelling `token` sends "$/cancelRequest": the request then fails with a cancelled error.
   */
  async request<P, R>(type: RequestType<P, R, void>, params: P, token?: CancellationToken): Promise<R> {
    await this.ready();
    return this.send(() => this.connection.sendRequest(type, params, token));
  }

  /** Sends a request without parameters (see `request`). */
  async request0<R>(type: RequestType0<R, void>, token?: CancellationToken): Promise<R> {
    await this.ready();
    return this.send(() => this.connection.sendRequest(type, token));
  }

  /**
   * Registers a listener for a notification sent by the server (any number of listeners per
   * notification). Returns a function to unregister it.
   */
  onNotification<P>(type: NotificationType<P>, listener: Listener<P>): () => void {
    let handlers = this.notificationHandlers.get(type.method);
    if (!handlers) {
      handlers = new Set();
      this.notificationHandlers.set(type.method, handlers);
    }
    const handler = listener as Listener<unknown>;
    handlers.add(handler);
    return () => {
      this.notificationHandlers.get(type.method)?.delete(handler);
    };
  }

  /** Registers a listener called (once) when the server process exits. */
  onExit(listener: Listener<string>): () => void {
    this.exitListeners.push(listener);
    return () => {
      const index = this.exitListeners.indexOf(listener);
      if (index >= 0) {
        this.exitListeners.splice(index, 1);
      }
    };
  }

  /**
   * Stops the server: closing stdin makes the server terminate gracefully. The process is killed
   * if it is still running after `timeoutMs`.
   */
  dispose(timeoutMs = 3000): Promise<void> {
    if (this.exitReason !== undefined) {
      return Promise.resolve();
    }
    return new Promise(resolve => {
      const timer = setTimeout(() => {
        this.process.kill();
        resolve();
      }, timeoutMs);
      this.process.once('exit', () => {
        clearTimeout(timer);
        resolve();
      });
      this.process.stdin.end();
    });
  }

  /** Sends a request, tracking it so that it fails with `ServerExitedError` if the server exits. */
  private send<R>(sendRequest: () => Promise<R>): Promise<R> {
    if (this.exitReason !== undefined) {
      return Promise.reject(new ServerExitedError(this.exitReason));
    }
    return new Promise<R>((resolve, reject) => {
      const entry: PendingRequest = { reject };
      this.pending.add(entry);
      sendRequest().then(
        result => {
          this.pending.delete(entry);
          resolve(result);
        },
        error => {
          this.pending.delete(entry);
          reject(toClientError(error, this.exitReason));
        });
    });
  }

  private dispatchNotification(method: string, params: unknown) {
    const handlers = this.notificationHandlers.get(method);
    if (!handlers || handlers.size === 0) {
      return; // Notifications not used by the extension (e.g. "index/scanStarted" in tests).
    }
    for (const handler of [...handlers]) {
      try {
        handler(params);
      } catch (e) {
        this.log(`Error handling notification "${method}": ${e instanceof Error ? e.stack ?? e.message : String(e)}`);
      }
    }
  }

  private handleExit(reason: string) {
    if (this.exitReason !== undefined) {
      return;
    }
    this.exitReason = reason;
    this.log(reason);
    this.rejectStartup?.(new ServerExitedError(reason));
    for (const pending of [...this.pending]) {
      pending.reject(new ServerExitedError(reason));
    }
    this.pending.clear();
    try {
      this.connection.dispose();
    } catch {
      // Already disposed.
    }
    for (const listener of [...this.exitListeners]) {
      listener(reason);
    }
  }

  private log(message: string) {
    this.options.log?.(message);
  }
}

/** Maps an error of vscode-jsonrpc to `ServerError` (JSON-RPC error) or `ServerExitedError`. */
function toClientError(error: unknown, exitReason: string | undefined): Error {
  if (exitReason !== undefined) {
    return new ServerExitedError(exitReason);
  }
  if (error instanceof ResponseError) {
    return new ServerError(error.code, error.message, error.data);
  }
  return error instanceof Error ? error : new Error(String(error));
}

/**
 * Linux/macOS: makes sure the server file is executable (the executable bit may be lost when the
 * extension is packaged on Windows).
 */
function ensureExecutable(serverPath: string): void {
  if (process.platform === 'win32') {
    return;
  }
  try {
    const mode = fs.statSync(serverPath).mode;
    if ((mode & 0o111) === 0) {
      fs.chmodSync(serverPath, mode | 0o755);
    }
  } catch {
    // Missing file, read-only file system...: spawn reports the error.
  }
}
