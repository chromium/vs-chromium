// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { NotificationType, RequestType, RequestType0 } from 'vscode-jsonrpc';
import { InitializeResult } from '../protocol/protocol.g';
import { DashboardBridge, DashboardServer } from '../ui/dashboard/dashboardBridge';
import type { HostCommand, HostState, HostToPage } from '../ui/dashboard/dashboardMessages';
import { sampleIndexStatistics } from './dashboardSampleData';

/** A fake server: records the requests, sends notifications, exits. */
class FakeDashboardServer implements DashboardServer {
  readonly requests: { method: string; params?: unknown }[] = [];
  readonly results = new Map<string, unknown>();
  private readonly listeners = new Map<string, Set<(params: unknown) => void>>();
  private readonly exitListeners = new Set<(reason: string) => void>();
  private resolveReady!: (info: InitializeResult) => void;
  private readonly readyPromise = new Promise<InitializeResult>(resolve => {
    this.resolveReady = resolve;
  });

  async request<P, R>(type: RequestType<P, R, void>, params: P): Promise<R> {
    this.requests.push({ method: type.method, params });
    const result = this.results.get(type.method);
    if (result instanceof Error) {
      throw result;
    }
    return result as R;
  }

  async request0<R>(type: RequestType0<R, void>): Promise<R> {
    this.requests.push({ method: type.method });
    return null as R;
  }

  onNotification<P>(type: NotificationType<P>, listener: (params: P) => void): () => void {
    let set = this.listeners.get(type.method);
    if (!set) {
      set = new Set();
      this.listeners.set(type.method, set);
    }
    const l = listener as (params: unknown) => void;
    set.add(l);
    return () => set.delete(l);
  }

  onExit(listener: (reason: string) => void): () => void {
    this.exitListeners.add(listener);
    return () => this.exitListeners.delete(listener);
  }

  ready(): Promise<InitializeResult> {
    return this.readyPromise;
  }

  start() {
    this.resolveReady({ serverVersion: '1.2.3', protocolVersion: 1, serverProcessId: 42 });
  }

  notify(method: string, params: unknown) {
    this.listeners.get(method)?.forEach(listener => listener(params));
  }

  get listenerCount(): number {
    return [...this.listeners.values()].reduce((n, set) => n + set.size, 0) + this.exitListeners.size;
  }

  exit(reason: string) {
    this.exitListeners.forEach(listener => listener(reason));
  }
}

const flush = () => new Promise(resolve => setImmediate(resolve));

function setup() {
  let server: FakeDashboardServer | undefined = new FakeDashboardServer();
  const posted: HostToPage[] = [];
  const commands: HostCommand[] = [];
  let setting = false;
  const bridge = new DashboardBridge({
    hostName: 'Test',
    getServer: () => server,
    post: message => posted.push(message),
    trigramFiltersSetting: () => setting,
    logDirectory: '/logs',
    executeCommand: async command => {
      commands.push(command);
      if (command.command === 'setTrigramFiltersSetting') {
        setting = command.enabled;
      }
    },
    errorMessage: e => (e instanceof Error ? e.message : String(e)),
  });
  const hostStates = () => posted.filter((m): m is Extract<HostToPage, { type: 'hostState' }> => m.type === 'hostState').map(m => m.state);
  return {
    bridge,
    posted,
    commands,
    hostStates,
    lastHostState: (): HostState => hostStates()[hostStates().length - 1],
    get server() {
      return server!;
    },
    setServer: (s: FakeDashboardServer | undefined) => {
      server = s;
    },
  };
}

describe('DashboardBridge', () => {
  it('sends the state of the host when the page is ready, with the server version once known', async () => {
    const context = setup();
    await context.bridge.handleMessage({ type: 'ready' });
    assert.deepEqual(context.lastHostState(), {
      hostName: 'Test',
      serverRunning: true,
      stoppedReason: undefined,
      serverVersion: undefined,
      trigramFiltersSetting: false,
      logDirectory: '/logs',
    });
    context.server.start();
    await flush();
    assert.equal(context.lastHostState().serverVersion, '1.2.3');
  });

  it('forwards the protocol requests of the page', async () => {
    const context = setup();
    context.server.results.set('index/statistics', sampleIndexStatistics());
    await context.bridge.handleMessage({ type: 'request', id: 7, method: 'index/statistics', params: {} });
    await context.bridge.handleMessage({ type: 'request', id: 8, method: 'index/pause' });
    await context.bridge.handleMessage({ type: 'request', id: 9, method: 'project/setOptions', params: { projectPath: 'C:\\a', trigramFiltersEnabled: true } });
    assert.deepEqual(context.server.requests, [
      { method: 'index/statistics', params: {} },
      { method: 'index/pause' },
      { method: 'project/setOptions', params: { projectPath: 'C:\\a', trigramFiltersEnabled: true } },
    ]);
    const responses = context.posted.filter(m => m.type === 'response');
    assert.deepEqual(responses, [
      { type: 'response', id: 7, result: sampleIndexStatistics() },
      { type: 'response', id: 8, result: null },
      { type: 'response', id: 9, result: null },
    ]);
  });

  it('rejects the methods not used by the dashboard', async () => {
    const context = setup();
    await context.bridge.handleMessage({ type: 'request', id: 1, method: 'search/code' as never, params: {} });
    assert.deepEqual(context.server.requests, []);
    assert.deepEqual(context.posted.filter(m => m.type === 'response'), [
      { type: 'response', id: 1, error: 'Request not allowed: search/code' },
    ]);
  });

  it('returns the errors of the server', async () => {
    const context = setup();
    context.server.results.set('server/statistics', new Error('Boom'));
    await context.bridge.handleMessage({ type: 'request', id: 3, method: 'server/statistics', params: {} });
    assert.deepEqual(context.posted.filter(m => m.type === 'response'), [{ type: 'response', id: 3, error: 'Boom' }]);
  });

  it('forwards the index notifications', async () => {
    const context = setup();
    await context.bridge.handleMessage({ type: 'ready' });
    context.server.notify('index/stateChanged', { status: 'paused', lastIndexUpdatedUtc: 'T' });
    context.server.notify('index/progress', { displayText: 'x', completed: 1, total: 2 });
    assert.deepEqual(context.posted.filter(m => m.type === 'notification'), [
      { type: 'notification', method: 'index/stateChanged', params: { status: 'paused', lastIndexUpdatedUtc: 'T' } },
      { type: 'notification', method: 'index/progress', params: { displayText: 'x', completed: 1, total: 2 } },
    ]);
  });

  it('tracks the exits and restarts of the server', async () => {
    const context = setup();
    await context.bridge.handleMessage({ type: 'ready' });
    const first = context.server;
    first.exit('exit code 1');
    assert.equal(context.lastHostState().serverRunning, false);
    assert.equal(context.lastHostState().stoppedReason, 'exit code 1');
    await context.bridge.handleMessage({ type: 'request', id: 1, method: 'index/statistics', params: {} });
    assert.deepEqual(context.posted.filter(m => m.type === 'response'),
      [{ type: 'response', id: 1, error: 'The indexing server is not running.' }]);

    // Restarted: the notifications of the new server are forwarded, not those of the old one.
    const second = new FakeDashboardServer();
    context.setServer(second);
    context.bridge.syncServer();
    assert.equal(context.lastHostState().serverRunning, true);
    assert.equal(first.listenerCount, 0);
    context.posted.length = 0;
    first.notify('index/loaded', { operationId: 1, treeVersion: 1 });
    second.notify('index/loaded', { operationId: 2, treeVersion: 2 });
    assert.deepEqual(context.posted, [{ type: 'notification', method: 'index/loaded', params: { operationId: 2, treeVersion: 2 } }]);

    context.setServer(undefined);
    context.bridge.syncServer();
    assert.equal(context.lastHostState().serverRunning, false);
  });

  it('executes the commands of the page and sends the new setting', async () => {
    const context = setup();
    await context.bridge.handleMessage({ type: 'ready' });
    await context.bridge.handleMessage({ type: 'command', command: 'setTrigramFiltersSetting', enabled: true });
    await context.bridge.handleMessage({ type: 'command', command: 'restartServer' });
    assert.deepEqual(context.commands, [{ command: 'setTrigramFiltersSetting', enabled: true }, { command: 'restartServer' }]);
    assert.equal(context.lastHostState().trigramFiltersSetting, true);
  });

  it('stops forwarding when disposed', async () => {
    const context = setup();
    await context.bridge.handleMessage({ type: 'ready' });
    const server = context.server;
    context.bridge.dispose();
    context.posted.length = 0;
    server.notify('index/loaded', { operationId: 1, treeVersion: 1 });
    assert.deepEqual(context.posted, []);
    assert.equal(server.listenerCount, 0);
  });
});
