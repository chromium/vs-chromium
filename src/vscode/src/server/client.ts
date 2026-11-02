// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import { ChildProcessWithoutNullStreams, spawn } from 'child_process';
import * as readline from 'readline';
import {
  ErrorResponse,
  IpcMessage,
  IpcProtocols,
  IpcRequest,
  IpcResponse,
  ResponseTypes,
  TypedEvent,
  TypedRequest,
} from './messages';

/** Error returned by the server in response to a request. */
export class ServerError extends Error {
  constructor(readonly response: ErrorResponse) {
    super(response.message ?? 'Unknown server error');
    this.name = 'ServerError';
  }
}

/** The server process exited (or could not be started) before a request completed. */
export class ServerExitedError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ServerExitedError';
  }
}

export interface ServerClientOptions {
  /** Called with diagnostic messages (server stderr, protocol errors, ...). */
  log?: (message: string) => void;
  /** Called with every message sent/received, for tracing. */
  trace?: (direction: 'send' | 'recv', json: string) => void;
}

type Listener<T> = (value: T) => void;

interface PendingRequest {
  resolve: (data: unknown) => void;
  reject: (error: Error) => void;
}

/**
 * Client for VsChromium.Server.exe running in "--stdio" mode: one JSON message per line on
 * stdin/stdout. Correlates responses with requests (by requestId) and dispatches server events.
 *
 * This class has no dependency on the VS Code API so it can be tested with plain Node.
 */
export class ServerClient {
  private readonly process: ChildProcessWithoutNullStreams;
  private readonly pending = new Map<number, PendingRequest>();
  private readonly eventListeners: Listener<TypedEvent>[] = [];
  private readonly exitListeners: Listener<string>[] = [];
  private readonly helloPromise: Promise<void>;
  // RequestId 1 is used by the "hello" response sent by the server at startup.
  private nextRequestId = 2;
  private exitReason: string | undefined;

  constructor(serverPath: string, private readonly options: ServerClientOptions = {}) {
    this.process = spawn(serverPath, ['--stdio'], { windowsHide: true, stdio: 'pipe' });

    let helloResolve!: () => void;
    let helloReject!: (e: Error) => void;
    this.helloPromise = new Promise<void>((resolve, reject) => {
      helloResolve = resolve;
      helloReject = reject;
    });
    // Avoid unhandled rejection warnings if nobody waits for "ready".
    this.helloPromise.catch(() => undefined);

    readline.createInterface({ input: this.process.stdout, crlfDelay: Infinity }).on('line', line => {
      if (line.trim().length === 0) {
        return;
      }
      this.options.trace?.('recv', line);
      let message: IpcMessage;
      try {
        message = JSON.parse(line);
      } catch (e) {
        this.log(`Ignoring malformed message from server: ${line}`);
        return;
      }
      if (message.protocol === IpcProtocols.hello) {
        helloResolve();
        return;
      }
      this.onMessage(message as IpcResponse);
    });

    readline.createInterface({ input: this.process.stderr, crlfDelay: Infinity }).on('line', line => {
      this.log(`[server stderr] ${line}`);
    });

    this.process.on('error', error => this.handleExit(`Failed to start server "${serverPath}": ${error.message}`));
    this.process.on('exit', (code, signal) =>
      this.handleExit(`Server process exited (code=${code ?? 'none'}, signal=${signal ?? 'none'})`));
    this.process.on('exit', () => helloReject(new ServerExitedError(this.exitReason ?? 'Server exited')));
    this.process.on('error', () => helloReject(new ServerExitedError(this.exitReason ?? 'Server failed to start')));
    // Writing to stdin after the process exited raises EPIPE errors: these are handled by onExit.
    this.process.stdin.on('error', error => this.log(`Error writing to server: ${error.message}`));
  }

  /** Process id of the server, if started. */
  get pid(): number | undefined {
    return this.process.pid;
  }

  /** True once the server process has exited (or failed to start). */
  get hasExited(): boolean {
    return this.exitReason !== undefined;
  }

  /** Resolves when the server is up and running (i.e. has sent the "hello" message). */
  ready(): Promise<void> {
    return this.helloPromise;
  }

  /** Sends a typed request to the server and returns the typed response. */
  request<T extends TypedRequest>(data: T, options?: { runOnSequentialQueue?: boolean }): Promise<ResponseTypes[T['$type']]> {
    if (this.exitReason !== undefined) {
      return Promise.reject(new ServerExitedError(this.exitReason));
    }
    const requestId = this.nextRequestId++;
    const request: IpcRequest = {
      $type: 'IpcRequest',
      requestId,
      protocol: IpcProtocols.typedMessage,
      runOnSequentialQueue: options?.runOnSequentialQueue ?? false,
      data,
    };
    return new Promise((resolve, reject) => {
      this.pending.set(requestId, { resolve: resolve as (data: unknown) => void, reject });
      this.send(request);
    });
  }

  /** Registers a listener for events sent by the server. Returns a function to unregister it. */
  onEvent(listener: Listener<TypedEvent>): () => void {
    return addListener(this.eventListeners, listener);
  }

  /** Registers a listener called (once) when the server process exits. */
  onExit(listener: Listener<string>): () => void {
    return addListener(this.exitListeners, listener);
  }

  private handleExit(reason: string) {
    if (this.exitReason !== undefined) {
      return;
    }
    this.exitReason = reason;
    this.log(reason);
    for (const pending of this.pending.values()) {
      pending.reject(new ServerExitedError(reason));
    }
    this.pending.clear();
    for (const listener of [...this.exitListeners]) {
      listener(reason);
    }
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

  private send(message: IpcMessage) {
    const json = JSON.stringify(message);
    this.options.trace?.('send', json);
    this.process.stdin.write(json + '\n');
  }

  private onMessage(message: IpcResponse) {
    const data = message.data;
    if (message.$type === 'IpcEvent') {
      if (data && data.$type !== 'IpcStringData' && data.$type !== 'ErrorResponse') {
        for (const listener of [...this.eventListeners]) {
          listener(data as TypedEvent);
        }
      }
      return;
    }

    const pending = this.pending.get(message.requestId);
    if (!pending) {
      this.log(`Ignoring response for unknown request id ${message.requestId}`);
      return;
    }
    this.pending.delete(message.requestId);
    if (data?.$type === 'ErrorResponse') {
      pending.reject(new ServerError(data as ErrorResponse));
    } else {
      pending.resolve(data);
    }
  }

  private log(message: string) {
    this.options.log?.(message);
  }
}

function addListener<T>(listeners: Listener<T>[], listener: Listener<T>): () => void {
  listeners.push(listener);
  return () => {
    const index = listeners.indexOf(listener);
    if (index >= 0) {
      listeners.splice(index, 1);
    }
  };
}
