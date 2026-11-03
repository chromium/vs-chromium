// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Unit tests of ServerClient against a fake server (see fakeServer.ts): initialization, error
// mapping, cancellation, notifications and server exit.

import * as assert from 'node:assert/strict';
import { afterEach, describe, it } from 'node:test';
import { CancellationTokenSource, ResponseError } from 'vscode-jsonrpc';
import {
  IndexLoadedNotification,
  IndexProgressNotification,
  IndexScanFinishedNotification,
  IndexScanStartedNotification,
  IndexStateChangedNotification,
  IndexStatisticsRequest,
  SearchCodeRequest,
} from '../protocol/protocol.g';
import {
  ErrorCodes,
  isCancelledError,
  ServerClient,
  ServerError,
  ServerExitedError,
  userErrorMessage,
} from '../server/client';
import { IndexEvent, IndexingState, nextIndexingState, subscribeIndexEvents } from '../ui/indexingState';
import { FakeServerProcess } from './fakeServer';

const searchParams = { query: { text: 'foo' }, maxResults: 10 };
const emptyStatistics = {
  projectCount: 0,
  fileCount: 0,
  searchableFileCount: 0,
  serverNativeMemoryUsage: 0,
  serverGcMemoryUsage: 0,
  fileContentsArenas: { arenaCount: 0, arenaBytes: 0, usedBytes: 0, liveBytes: 0, sparseArenaCount: 0 },
  indexLastUpdatedUtc: '2026-01-01T00:00:00Z',
  status: 'idle' as const,
};

function waitFor(predicate: () => boolean, timeoutMs = 5000): Promise<void> {
  const started = Date.now();
  return new Promise((resolve, reject) => {
    const check = () => {
      if (predicate()) {
        resolve();
      } else if (Date.now() - started > timeoutMs) {
        reject(new Error('Timeout'));
      } else {
        setTimeout(check, 5);
      }
    };
    check();
  });
}

describe('ServerClient (fake server)', () => {
  const clients: ServerClient[] = [];
  const createClient = (server: FakeServerProcess, logs?: string[]) => {
    const client = new ServerClient(server, { clientVersion: '1.2.3', log: m => logs?.push(m) });
    clients.push(client);
    return client;
  };

  afterEach(async () => {
    await Promise.all(clients.splice(0).map(c => c.dispose(1000)));
  });

  describe('initialize', () => {
    it('sends server/initialize first, with the client name and versions', async () => {
      const server = new FakeServerProcess();
      server.connection.onRequest(IndexStatisticsRequest.type, () => emptyStatistics);
      const client = createClient(server);
      // Sent immediately: must still be sent after server/initialize.
      const stats = client.request(IndexStatisticsRequest.type, {});

      const result = await client.ready();
      assert.deepEqual(result, { serverVersion: '1.0.0-fake', protocolVersion: 1, serverProcessId: 1234 });
      assert.deepEqual(server.initializeParams, { clientName: 'VS Code', clientVersion: '1.2.3', protocolVersion: 1 });
      assert.equal((await stats).status, 'idle');
      assert.deepEqual(server.receivedMethods, ['server/initialize', 'index/statistics']);
    });

    it('fails when the server implements another version of the protocol', async () => {
      const server = new FakeServerProcess({ protocolVersion: 2 });
      const client = createClient(server);
      await assert.rejects(client.ready(), /version 2 of the protocol/);
      await assert.rejects(client.request(IndexStatisticsRequest.type, {}), /version 2 of the protocol/);
    });
  });

  describe('errors', () => {
    const requestFailingWith = async (error: ResponseError<unknown>): Promise<unknown> => {
      const server = new FakeServerProcess();
      server.connection.onRequest(SearchCodeRequest.type, () => {
        throw error;
      });
      const client = createClient(server);
      try {
        await client.request(SearchCodeRequest.type, searchParams);
      } catch (e) {
        return e;
      }
      assert.fail('The request should fail');
    };

    it('shows recoverable errors (-32001) to the user as is', async () => {
      const e = await requestFailingWith(new ResponseError(ErrorCodes.recoverableError, 'Invalid regular expression "("'));
      assert.ok(e instanceof ServerError);
      assert.equal(e.code, -32001);
      assert.equal(isCancelledError(e), false);
      assert.equal(userErrorMessage(e), 'Invalid regular expression "("');
    });

    it('ignores cancelled requests (-32800)', async () => {
      const e = await requestFailingWith(new ResponseError(ErrorCodes.requestCancelled, 'Request cancelled'));
      assert.ok(e instanceof ServerError);
      assert.equal(isCancelledError(e), true);
      assert.equal(userErrorMessage(e), undefined);
    });

    it('reports internal errors (-32603) and other errors with context', async () => {
      const internal = await requestFailingWith(new ResponseError(ErrorCodes.internalError, 'Object reference not set', { message: 'x' }));
      assert.ok(internal instanceof ServerError);
      assert.deepEqual(internal.data, { message: 'x' });
      assert.equal(userErrorMessage(internal), 'Internal error of the indexing server: Object reference not set');

      const invalidParams = await requestFailingWith(new ResponseError(-32602, 'Missing "query"'));
      assert.equal(userErrorMessage(invalidParams), 'Missing "query" (error -32602)');
    });

    it('maps other errors to their message', () => {
      assert.equal(userErrorMessage(new ServerExitedError('Server process exited (code=1, signal=none)')),
        'Server process exited (code=1, signal=none)');
      assert.equal(userErrorMessage('text'), 'text');
    });
  });

  describe('cancellation', () => {
    it('sends $/cancelRequest when the token is cancelled', async () => {
      const server = new FakeServerProcess();
      let started = false;
      server.connection.onRequest(SearchCodeRequest.type, (_params, token) => new Promise((_resolve, reject) => {
        started = true;
        token.onCancellationRequested(() => reject(new ResponseError(ErrorCodes.requestCancelled, 'Request cancelled')));
      }));
      const client = createClient(server);
      const cancellation = new CancellationTokenSource();
      const search = client.request(SearchCodeRequest.type, searchParams, cancellation.token);
      await waitFor(() => started);
      cancellation.cancel();
      await assert.rejects(search, e => isCancelledError(e));
      assert.ok(server.receivedMethods.includes('$/cancelRequest'));
    });
  });

  describe('notifications', () => {
    it('dispatches notifications to all the listeners until they unsubscribe', async () => {
      const server = new FakeServerProcess();
      const client = createClient(server);
      await client.ready();
      const first: number[] = [];
      const second: number[] = [];
      const unsubscribeFirst = client.onNotification(IndexScanStartedNotification.type, p => first.push(p.operationId));
      client.onNotification(IndexScanStartedNotification.type, p => second.push(p.operationId));

      await server.connection.sendNotification(IndexScanStartedNotification.type, { operationId: 1 });
      await waitFor(() => second.length === 1);
      unsubscribeFirst();
      await server.connection.sendNotification(IndexScanStartedNotification.type, { operationId: 2 });
      await waitFor(() => second.length === 2);
      assert.deepEqual(first, [1]);
      assert.deepEqual(second, [1, 2]);
    });

    it('updates the indexing state from the index notifications', async () => {
      const server = new FakeServerProcess();
      const client = createClient(server);
      await client.ready();
      let state: IndexingState = { kind: 'starting' };
      const events: IndexEvent['type'][] = [];
      subscribeIndexEvents(client, event => {
        events.push(event.type);
        state = nextIndexingState(state, event) ?? state;
      });

      const send = server.connection;
      await send.sendNotification(IndexScanStartedNotification.type, { operationId: 1 });
      await waitFor(() => state.kind === 'scanning');
      await send.sendNotification(IndexScanFinishedNotification.type, { operationId: 1, oldVersion: 0, newVersion: 1 });
      await send.sendNotification(IndexProgressNotification.type, { displayText: 'Loading files', completed: 1, total: 4 });
      await waitFor(() => state.kind === 'loading' && state.progress !== undefined);
      assert.deepEqual(state, { kind: 'loading', progress: { phase: 'loading files', percent: 25 } });
      await send.sendNotification(IndexLoadedNotification.type, { operationId: 2, treeVersion: 1 });
      await waitFor(() => state.kind === 'ready');
      await send.sendNotification(IndexStateChangedNotification.type, { status: 'yield', lastIndexUpdatedUtc: '2026-01-01T00:00:00Z' });
      await waitFor(() => state.kind === 'paused');
      assert.deepEqual(state, { kind: 'paused', yielded: true });
      assert.deepEqual(events, ['scanStarted', 'scanFinished', 'progress', 'loaded', 'stateChanged']);
    });
  });

  describe('server exit', () => {
    it('rejects pending and new requests with ServerExitedError and notifies the exit listeners', async () => {
      const server = new FakeServerProcess();
      let started = false;
      server.connection.onRequest(SearchCodeRequest.type, () => {
        started = true;
        return new Promise<never>(() => undefined); // Never answers.
      });
      const client = createClient(server);
      const exited = new Promise<string>(resolve => client.onExit(resolve));
      const search = client.request(SearchCodeRequest.type, searchParams);
      await waitFor(() => started);

      server.exit(3); // Crash.
      await assert.rejects(search, ServerExitedError);
      assert.match(await exited, /exited \(code=3/);
      assert.ok(client.hasExited);
      await assert.rejects(client.request(IndexStatisticsRequest.type, {}), ServerExitedError);
    });

    it('stops the server by closing its standard input', async () => {
      const server = new FakeServerProcess();
      const client = createClient(server);
      await client.ready();
      await client.dispose();
      assert.ok(client.hasExited);
    });
  });
});
