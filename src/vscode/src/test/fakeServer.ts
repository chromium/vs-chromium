// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// A fake VsChromium.Server process for unit tests of ServerClient: the "server" side is a
// vscode-jsonrpc connection over in-memory streams, with handlers defined by each test.

import { EventEmitter } from 'node:events';
import { PassThrough } from 'node:stream';
import { MessageConnection } from 'vscode-jsonrpc';
import { createMessageConnection, StreamMessageReader, StreamMessageWriter } from 'vscode-jsonrpc/node';
import { InitializeParams, ServerInitializeRequest } from '../protocol/protocol.g';
import { ServerProcess } from '../server/client';

export class FakeServerProcess extends EventEmitter implements ServerProcess {
  /** Written by the client. */
  readonly stdin = new PassThrough();
  /** Written by the fake server. */
  readonly stdout = new PassThrough();
  readonly pid = 1234;
  /** The connection of the fake server: register request handlers, send notifications. */
  readonly connection: MessageConnection;
  /** The methods of the requests received, in order. */
  readonly receivedMethods: string[] = [];
  initializeParams: InitializeParams | undefined;
  private exited = false;

  constructor(options: { protocolVersion?: number } = {}) {
    super();
    this.connection = createMessageConnection(new StreamMessageReader(this.stdin), new StreamMessageWriter(this.stdout));
    this.connection.onRequest(ServerInitializeRequest.type, params => {
      this.initializeParams = params;
      return { serverVersion: '1.0.0-fake', protocolVersion: options.protocolVersion ?? 1, serverProcessId: this.pid };
    });
    // The server exits when its standard input is closed.
    this.stdin.on('finish', () => this.exit(0));
    this.connection.listen();
    // Record the order of the requests (before they are dispatched to the handlers).
    this.stdin.on('data', (chunk: Buffer) => {
      for (const match of chunk.toString('utf8').matchAll(/"method":"([^"]+)"/g)) {
        this.receivedMethods.push(match[1]);
      }
    });
  }

  kill(): void {
    this.exit(null, 'SIGTERM');
  }

  /** Simulates the exit (or crash) of the server process. */
  exit(code: number | null, signal: string | null = null): void {
    if (this.exited) {
      return;
    }
    this.exited = true;
    this.connection.dispose();
    this.stdout.end();
    setImmediate(() => this.emit('exit', code, signal));
  }
}
