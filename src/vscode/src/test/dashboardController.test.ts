// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { DashboardController, NotificationRefreshDelayMs, Timers } from '../ui/dashboard/dashboardController';
import type { HostState, HostToPage, PageToHost } from '../ui/dashboard/dashboardMessages';
import type { DashboardViewModel } from '../ui/dashboard/dashboardModel';
import { RefreshIntervalMs } from '../ui/dashboard/dashboardModel';
import { createHostShim, HostShim, ShimWindow } from '../ui/dashboard/hostShim';
import { historyOf, sampleIndexDetails, sampleIndexStatistics, sampleIndexStatus, sampleServerStatistics } from './dashboardSampleData';

/** The requests of a full refresh (the first one, after events). */
const FullRefresh = ['index/status', 'index/statistics', 'server/statistics', 'server/history', 'index/details'];

type Listener = (event: { data: unknown }) => void;

/** A fake event target ("message" events only). */
class FakeTarget {
  readonly listeners = new Set<Listener>();
  addEventListener(_type: 'message', listener: Listener) {
    this.listeners.add(listener);
  }
  removeEventListener(_type: 'message', listener: Listener) {
    this.listeners.delete(listener);
  }
  dispatch(data: unknown) {
    for (const listener of [...this.listeners]) {
      listener({ data });
    }
  }
}

describe('createHostShim', () => {
  it('uses the VS Code webview API', () => {
    const window = new FakeTarget() as FakeTarget & ShimWindow;
    const posted: unknown[] = [];
    let acquired = 0;
    window.acquireVsCodeApi = () => {
      acquired++;
      return { postMessage: (message: unknown) => posted.push(message) };
    };
    const shim = createHostShim(window);
    assert.equal(shim.kind, 'vscode');
    assert.equal(acquired, 1);
    shim.post({ type: 'ready' });
    assert.deepEqual(posted, [{ type: 'ready' }]);

    const received: HostToPage[] = [];
    const unsubscribe = shim.onMessage(message => received.push(message));
    window.dispatch({ type: 'visibility', visible: false });
    window.dispatch('not a message');
    window.dispatch(null);
    unsubscribe();
    window.dispatch({ type: 'visibility', visible: true });
    assert.deepEqual(received, [{ type: 'visibility', visible: false }]);
  });

  it('uses the WebView2 API', () => {
    const window = new FakeTarget() as FakeTarget & ShimWindow;
    const webview = new FakeTarget() as FakeTarget & { postMessage(message: unknown): void };
    const posted: unknown[] = [];
    webview.postMessage = message => posted.push(message);
    window.chrome = { webview };
    const shim = createHostShim(window);
    assert.equal(shim.kind, 'webview2');
    shim.post({ type: 'command', command: 'showOutput' });
    assert.deepEqual(posted, [{ type: 'command', command: 'showOutput' }]);

    const received: HostToPage[] = [];
    shim.onMessage(message => received.push(message));
    window.dispatch({ type: 'visibility', visible: false }); // Not the WebView2 channel.
    webview.dispatch({ type: 'visibility', visible: true });
    assert.deepEqual(received, [{ type: 'visibility', visible: true }]);
  });

  it('fails without a host', () => {
    assert.throws(() => createHostShim(new FakeTarget()), /not hosted/);
  });
});

/** Timers run explicitly by the tests. */
class FakeTimers implements Timers {
  private nextId = 1;
  readonly timers = new Map<number, { callback: () => void; ms: number }>();

  setTimeout(callback: () => void, ms: number): unknown {
    const id = this.nextId++;
    this.timers.set(id, { callback, ms });
    return id;
  }

  clearTimeout(handle: unknown): void {
    this.timers.delete(handle as number);
  }

  /** The delays of the pending timers, except the request timeouts. */
  get delays(): number[] {
    return [...this.timers.values()].map(t => t.ms).filter(ms => ms < 30_000);
  }

  /** Runs the pending timers with the given delay. */
  run(ms: number) {
    for (const [id, timer] of [...this.timers]) {
      if (timer.ms === ms) {
        this.timers.delete(id);
        timer.callback();
      }
    }
  }
}

/** A fake host: records the messages of the page, answers its requests. */
class FakeHost implements HostShim {
  readonly kind = 'vscode';
  readonly posted: PageToHost[] = [];
  private handler: ((message: HostToPage) => void) | undefined;

  post(message: PageToHost): void {
    this.posted.push(message);
  }

  onMessage(handler: (message: HostToPage) => void): () => void {
    this.handler = handler;
    return () => {
      this.handler = undefined;
    };
  }

  send(message: HostToPage) {
    this.handler?.(message);
  }

  /** The requests posted since the last call. */
  takeRequests(): Extract<PageToHost, { type: 'request' }>[] {
    const requests = this.posted.filter((m): m is Extract<PageToHost, { type: 'request' }> => m.type === 'request');
    this.posted.length = 0;
    return requests;
  }

  /** Answers the requests with the sample data. */
  answer(requests: Extract<PageToHost, { type: 'request' }>[], results: Record<string, unknown> = {}) {
    const defaults: Record<string, unknown> = {
      'index/status': sampleIndexStatus(),
      'index/statistics': sampleIndexStatistics(),
      'server/history': historyOf([{ time: 1_000_000 }, { time: 1_010_000 }]),
      'server/statistics': sampleServerStatistics(),
      'index/details': sampleIndexDetails(),
    };
    for (const request of requests) {
      const result = results[request.method] ?? defaults[request.method] ?? null;
      if (result instanceof Error) {
        this.send({ type: 'response', id: request.id, error: result.message });
      } else {
        this.send({ type: 'response', id: request.id, result });
      }
    }
  }
}

const running: HostState = {
  hostName: 'Test',
  serverRunning: true,
  serverVersion: '1.2.3',
  trigramFiltersSetting: false,
  logDirectory: '/logs',
};

const flush = () => new Promise(resolve => setImmediate(resolve));

function setup() {
  const host = new FakeHost();
  const timers = new FakeTimers();
  const renders: DashboardViewModel[] = [];
  const controller = new DashboardController(host, vm => renders.push(vm), timers, { formatTime: utc => utc });
  controller.start();
  return { host, timers, renders, controller, last: () => renders[renders.length - 1] };
}

/** Starts a controller with a running server and completes its first refresh. */
async function setupRunning() {
  const context = setup();
  context.host.send({ type: 'hostState', state: running });
  context.timers.run(0);
  context.host.answer(context.host.takeRequests());
  await flush();
  return context;
}

describe('DashboardController', () => {
  it('tells the host it is ready and renders the initial state', () => {
    const { host, last } = setup();
    assert.deepEqual(host.posted, [{ type: 'ready' }]);
    assert.equal(last().status.kind, 'unknown');
    assert.equal(last().actions.restartServer, false);
  });

  it('requests everything first, then the index statistics every 2 seconds', async () => {
    const { host, timers, last } = setup();
    host.posted.length = 0;
    host.send({ type: 'hostState', state: running });
    assert.deepEqual(timers.delays, [0]);
    timers.run(0);
    const first = host.takeRequests();
    assert.deepEqual(first.map(r => r.method), FullRefresh);
    assert.deepEqual(first[3].params, {});
    assert.deepEqual(first[4].params, { maxFilesByExtensionDetailsCount: 0, maxLargeFilesDetailsCount: 0 });
    host.answer(first);
    await flush();
    assert.equal(last().status.text, 'Idle');
    assert.equal(last().projects.length, 1);
    assert.equal(last().operations.length, 2, 'without the search times');
    assert.deepEqual(timers.delays, [RefreshIntervalMs]);

    timers.run(RefreshIntervalMs);
    const second = host.takeRequests();
    assert.deepEqual(second.map(r => r.method), ['index/status']);
    host.answer(second, { 'index/status': sampleIndexStatus({ status: 'busy' }) });
    await flush();
    assert.equal(last().status.text, 'Indexing');
    assert.equal(last().projects.length, 1, 'details kept');
    assert.deepEqual(timers.delays, [RefreshIntervalMs]);
  });

  it('stops refreshing while hidden', async () => {
    const { host, timers, controller } = await setupRunning();
    host.send({ type: 'visibility', visible: false });
    assert.deepEqual(timers.delays, []);
    assert.equal(controller.visible, false);
    host.send({ type: 'visibility', visible: true });
    assert.deepEqual(timers.delays, [0]);
    controller.setPageVisible(false);
    assert.deepEqual(timers.delays, []);
    controller.setPageVisible(true);
    timers.run(0);
    // Everything is requested again when the page becomes visible.
    assert.deepEqual(host.takeRequests().map(r => r.method), FullRefresh);
  });

  it('updates the status from the notifications and refreshes soon', async () => {
    const { host, timers, last } = await setupRunning();
    host.send({ type: 'notification', method: 'index/stateChanged', params: { status: 'paused', lastIndexUpdatedUtc: 'T' } });
    assert.equal(last().status.text, 'Paused');
    assert.equal(last().pauseOrResume, 'resume');
    assert.equal(last().actions.resume, true);
    assert.deepEqual(timers.delays, [NotificationRefreshDelayMs]);

    host.send({ type: 'notification', method: 'index/stateChanged', params: { status: 'busy', lastIndexUpdatedUtc: 'T' } });
    host.send({ type: 'notification', method: 'index/progress', params: { displayText: 'Reading', completed: 1, total: 4 } });
    assert.equal(last().status.text, 'Indexing 25%');
    assert.deepEqual(timers.delays, [NotificationRefreshDelayMs], 'a single refresh');

    host.send({ type: 'notification', method: 'index/loaded', params: { operationId: 1, treeVersion: 2 } });
    timers.run(NotificationRefreshDelayMs);
    assert.deepEqual(host.takeRequests().map(r => r.method), FullRefresh);
  });

  it('sends the protocol requests of the actions, then refreshes', async () => {
    const { host, timers, controller } = await setupRunning();
    for (const [action, method] of [['pause', 'index/pause'], ['resume', 'index/resume'], ['rebuild', 'tree/refresh']] as const) {
      host.posted.length = 0;
      timers.timers.clear();
      const done = controller.execute({ action });
      const requests = host.takeRequests();
      assert.deepEqual(requests.map(r => r.method), [method]);
      host.answer(requests);
      await done;
      assert.deepEqual(timers.delays, [NotificationRefreshDelayMs]);
      timers.run(NotificationRefreshDelayMs);
      const refresh = host.takeRequests();
      assert.deepEqual(refresh.map(r => r.method), FullRefresh);
      host.answer(refresh);
      await flush();
    }
  });

  it('sends the host commands', async () => {
    const { host, controller } = await setupRunning();
    host.posted.length = 0;
    await controller.execute({ action: 'restartServer' });
    await controller.execute({ action: 'showOutput' });
    await controller.execute({ action: 'openLogDirectory' });
    await controller.execute({ action: 'toggleTrigramFiltersSetting' });
    await controller.execute({ action: 'setProjectTrigramFilters', projectPath: 'C:\\a', enabled: true });
    assert.deepEqual(host.posted.filter(m => m.type === 'command'), [
      { type: 'command', command: 'restartServer' },
      { type: 'command', command: 'showOutput' },
      { type: 'command', command: 'openLogDirectory' },
      { type: 'command', command: 'setTrigramFiltersSetting', enabled: true },
      { type: 'command', command: 'setProjectTrigramFilters', projectPath: 'C:\\a', enabled: true },
    ]);
  });

  it('shows the errors of the requests', async () => {
    const { host, timers, last } = await setupRunning();
    timers.run(RefreshIntervalMs);
    host.answer(host.takeRequests(), { 'index/status': new Error('Server error') });
    await flush();
    assert.equal(last().error, 'Server error');
    assert.deepEqual(timers.delays, [RefreshIntervalMs], 'refreshes continue');

    timers.run(RefreshIntervalMs);
    host.answer(host.takeRequests());
    await flush();
    assert.equal(last().error, undefined);
  });

  it('shows the errors of the actions', async () => {
    const { host, controller, last } = await setupRunning();
    host.posted.length = 0;
    const done = controller.execute({ action: 'pause' });
    host.answer(host.takeRequests(), { 'index/pause': new Error('Cannot pause') });
    await done;
    assert.equal(last().error, 'Cannot pause');
  });

  it('clears the data when the server stops and refreshes when it restarts', async () => {
    const { host, timers, last } = await setupRunning();
    host.send({ type: 'hostState', state: { ...running, serverRunning: false, stoppedReason: 'crashed' } });
    assert.deepEqual(last().status, { kind: 'stopped', text: 'Server not running (crashed)' });
    assert.deepEqual(last().projects, []);
    assert.equal(last().actions.pause, false);
    assert.equal(last().actions.restartServer, true);
    timers.run(RefreshIntervalMs);
    assert.deepEqual(host.takeRequests(), [], 'no requests while stopped');

    host.send({ type: 'hostState', state: running });
    timers.run(0);
    assert.deepEqual(host.takeRequests().map(r => r.method), FullRefresh);
  });

  it('polls the history incrementally and merges it', async () => {
    const { host, timers, last } = setup();
    host.send({ type: 'hostState', state: running });
    timers.run(0);
    host.answer(host.takeRequests());
    await flush();
    assert.deepEqual(last().memoryChart?.times, [1_000_000, 1_010_000]);
    for (let i = 1; i < 5; i++) {
      timers.run(RefreshIntervalMs);
      const requests = host.takeRequests();
      assert.deepEqual(requests.map(r => r.method), ['index/status']);
      host.answer(requests);
      await flush();
    }
    timers.run(RefreshIntervalMs);
    const requests = host.takeRequests();
    assert.deepEqual(requests.map(r => r.method), FullRefresh);
    assert.deepEqual(requests[3].params, { sinceUnixMs: 1_010_000 });
    host.answer(requests, { 'server/history': historyOf([{ time: 1_020_000 }]) });
    await flush();
    assert.deepEqual(last().memoryChart?.times, [1_000_000, 1_010_000, 1_020_000]);
    assert.equal(last().memoryChart?.version.includes(':3:'), true);
  });

  it('starts the history again with another server process', async () => {
    const { host, timers, last } = await setupRunning();
    assert.equal(last().memoryChart?.times.length, 2);
    host.send({ type: 'notification', method: 'index/loaded', params: { operationId: 1, treeVersion: 2 } });
    timers.run(NotificationRefreshDelayMs);
    const requests = host.takeRequests();
    assert.deepEqual(requests[3].params, { sinceUnixMs: 1_010_000 });
    host.answer(requests, {
      'server/statistics': sampleServerStatistics({ processId: 7 }),
      'server/history': historyOf([{ time: 2_000_000 }]),
    });
    await flush();
    assert.deepEqual(last().memoryChart?.times, [2_000_000]);
  });

  it('switches to the status of the last "index/status"', async () => {
    const { host, timers, last } = await setupRunning();
    timers.run(RefreshIntervalMs);
    host.answer(host.takeRequests(), { 'index/status': sampleIndexStatus({ status: 'paused', fileCount: 12 }) });
    await flush();
    assert.equal(last().status.text, 'Paused');
    assert.equal(last().facts[1], '12 files');
    assert.equal(last().memory.length > 0, true, 'arena statistics kept');
  });

  it('sends the cache commands', async () => {
    const { host, controller } = await setupRunning();
    host.posted.length = 0;
    await controller.execute({ action: 'clearCache' });
    await controller.execute({ action: 'clearCache', projectPath: 'C:\\a' });
    await controller.execute({ action: 'openCacheSettings' });
    assert.deepEqual(host.posted.filter(m => m.type === 'command'), [
      { type: 'command', command: 'clearCache' },
      { type: 'command', command: 'clearCache', projectPath: 'C:\\a' },
      { type: 'command', command: 'openCacheSettings' },
    ]);
  });

  it('sends the project commands, then refreshes the project details', async () => {
    const { host, timers, controller } = await setupRunning();
    host.posted.length = 0;
    timers.timers.clear();
    await controller.execute({ action: 'rebuildProject', projectPath: 'C:\\a' });
    await controller.execute({ action: 'setProjectCacheEnabled', projectPath: 'C:\\a', enabled: false });
    await controller.execute({ action: 'setProjectTrigramFilters', projectPath: 'C:\\a', enabled: true });
    await controller.execute({ action: 'openProjectCacheFolder', projectPath: 'C:\\a' });
    assert.deepEqual(host.posted.filter(m => m.type === 'command'), [
      { type: 'command', command: 'rebuildProject', projectPath: 'C:\\a' },
      { type: 'command', command: 'setProjectCacheEnabled', projectPath: 'C:\\a', enabled: false },
      { type: 'command', command: 'setProjectTrigramFilters', projectPath: 'C:\\a', enabled: true },
      { type: 'command', command: 'openProjectCacheFolder', projectPath: 'C:\\a' },
    ]);
    assert.deepEqual(timers.delays, [NotificationRefreshDelayMs]);
    timers.run(NotificationRefreshDelayMs);
    assert.deepEqual(host.takeRequests().map(r => r.method), FullRefresh);
  });

  it('fails the pending requests when disposed', async () => {
    const { host, controller, timers } = await setupRunning();
    timers.run(RefreshIntervalMs);
    assert.equal(host.takeRequests().length, 1);
    controller.dispose();
    await flush();
    assert.deepEqual(timers.timers.size, 0);
  });
});
