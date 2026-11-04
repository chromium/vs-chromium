// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { IndexingStatus } from '../protocol/protocol.g';
import type { HostState } from '../ui/dashboard/dashboardMessages';
import {
  buildViewModel,
  cpuUsagePercent,
  describeStatus,
  enabledActions,
  formatBytes,
  formatCount,
  formatDuration,
  planRefresh,
} from '../ui/dashboard/dashboardModel';
import { serverLogDirectory } from '../ui/dashboard/logDirectory';
import { sampleIndexDetails, sampleIndexStatistics, sampleProjectDetails, sampleServerStatistics } from './dashboardSampleData';

const time = (utc: string) => `time(${utc})`;
const options = { formatTime: time };

function host(state: Partial<HostState> = {}): HostState {
  return {
    hostName: 'Test',
    serverRunning: true,
    serverVersion: '1.2.3',
    trigramFiltersSetting: false,
    logDirectory: '/logs',
    ...state,
  };
}

describe('dashboard formatting', () => {
  it('formats bytes with a unit', () => {
    assert.equal(formatBytes(512), '512 B');
    assert.equal(formatBytes(1536), '1.5 KB');
    assert.equal(formatBytes(523.4 * 1024 * 1024), '523.4 MB');
    assert.equal(formatBytes(14 * 1024 * 1024 * 1024), '14.0 GB');
    assert.equal(formatBytes(1500 * 1024 * 1024 * 1024), '1,500.0 GB');
  });

  it('formats counts', () => {
    assert.equal(formatCount(2_653_112), '2,653,112');
  });

  it('formats durations', () => {
    assert.equal(formatDuration(250.4), '250 ms');
    assert.equal(formatDuration(4_900), '4.9 s');
    assert.equal(formatDuration(133_000), '2 min 13 s');
    assert.equal(formatDuration((2 * 3600 + 13 * 60 + 5) * 1000), '2 h 13 min');
  });
});

describe('describeStatus', () => {
  it('describes the host and server states', () => {
    assert.deepEqual(describeStatus({}), { kind: 'unknown', text: 'Connecting\u2026' });
    assert.deepEqual(describeStatus({ host: host({ serverRunning: false, stoppedReason: 'exit code 1' }) }),
      { kind: 'stopped', text: 'Server not running (exit code 1)' });
    assert.deepEqual(describeStatus({ host: host() }), { kind: 'unknown', text: 'Loading\u2026' });
  });

  it('describes the indexing status', () => {
    const status = (s: IndexingStatus) => describeStatus({ host: host(), index: sampleIndexStatistics({ status: s }) });
    assert.deepEqual(status('idle'), { kind: 'ok', text: 'Idle' });
    assert.deepEqual(status('busy'), { kind: 'busy', text: 'Indexing' });
    assert.deepEqual(status('verifying'), { kind: 'busy', text: 'Verifying the index of the last session' });
    assert.deepEqual(status('paused'), { kind: 'paused', text: 'Paused' });
    assert.equal(status('yield').kind, 'paused');
  });

  it('shows the progress of indexing', () => {
    const data = {
      host: host(),
      index: sampleIndexStatistics({ status: 'busy' }),
      progress: { displayText: 'Reading file x', completed: 455, total: 1000 },
    };
    assert.equal(describeStatus(data).text, 'Indexing 45%');
  });
});

describe('enabledActions', () => {
  const enabled = (status: IndexingStatus | undefined, state: Partial<HostState> = {}) => {
    const actions = enabledActions(host(state), status);
    return Object.entries(actions).filter(([, value]) => value).map(([key]) => key).sort();
  };
  const always = ['openLogDirectory', 'restartServer', 'showOutput'];

  it('enables pause and rebuild when idle', () => {
    assert.deepEqual(enabled('idle'), [...always, 'pause', 'rebuild', 'toggleTrigramFilters'].sort());
  });

  it('enables only pause while indexing', () => {
    assert.deepEqual(enabled('busy'), [...always, 'pause', 'toggleTrigramFilters'].sort());
    assert.deepEqual(enabled('verifying'), [...always, 'pause', 'rebuild', 'toggleTrigramFilters'].sort());
  });

  it('enables resume only when paused', () => {
    assert.deepEqual(enabled('paused'), [...always, 'resume', 'toggleTrigramFilters'].sort());
    assert.deepEqual(enabled('yield'), [...always, 'resume', 'toggleTrigramFilters'].sort());
    for (const status of ['idle', 'busy', 'verifying'] as IndexingStatus[]) {
      assert.equal(enabledActions(host(), status).resume, false);
    }
  });

  it('enables only restart and logs when the server is not running or the status is unknown', () => {
    assert.deepEqual(enabled('idle', { serverRunning: false }), always);
    assert.deepEqual(enabled(undefined), [...always, 'toggleTrigramFilters'].sort());
    assert.deepEqual(enabled(undefined, { logDirectory: undefined, serverRunning: false }), ['restartServer', 'showOutput']);
    assert.deepEqual(Object.values(enabledActions(undefined, undefined)).filter(x => x), []);
  });

  it('enables clearing the cache when the server has one', () => {
    assert.equal(enabledActions(host(), 'idle', true).clearCache, true);
    assert.equal(enabledActions(host(), 'busy', true).clearCache, true);
    assert.equal(enabledActions(host(), 'idle', false).clearCache, false);
    assert.equal(enabledActions(host({ serverRunning: false }), 'idle', true).clearCache, false);
    assert.equal(enabledActions(host(), undefined, true).clearCache, false);
  });

  it('enables the cache settings when the host has some', () => {
    assert.equal(enabledActions(host(), 'idle').openCacheSettings, false);
    assert.equal(enabledActions(host({ cacheSettings: { enabled: true, directory: '' } }), 'idle').openCacheSettings, true);
    assert.equal(enabledActions(host({ serverRunning: false, cacheSettings: { enabled: true, directory: '' } }), undefined).openCacheSettings, true);
  });
});

describe('planRefresh', () => {
  it('requests the statistics and the history every 5 refreshes and the details every 15', () => {
    const plans = Array.from({ length: 16 }, (_, i) => planRefresh(i));
    assert.deepEqual(plans.map((p, i) => p.statistics ? i : -1).filter(i => i >= 0), [0, 5, 10, 15]);
    assert.deepEqual(plans.map((p, i) => p.details ? i : -1).filter(i => i >= 0), [0, 15]);
  });

  it('requests what is forced', () => {
    assert.deepEqual(planRefresh(1, { details: true }), { statistics: false, details: true });
    assert.deepEqual(planRefresh(1, { statistics: true }), { statistics: true, details: false });
  });
});

describe('cpuUsagePercent', () => {
  it('computes the usage of all the processors between two statistics', () => {
    const previous = sampleServerStatistics({ uptimeMs: 10_000, cpuTotalMs: 1_000, processorCount: 4 });
    const current = sampleServerStatistics({ uptimeMs: 20_000, cpuTotalMs: 11_000, processorCount: 4 });
    assert.equal(cpuUsagePercent(previous, current), 25);
  });

  it('is unknown without previous statistics of the same process', () => {
    assert.equal(cpuUsagePercent(undefined, sampleServerStatistics()), undefined);
    assert.equal(cpuUsagePercent(sampleServerStatistics({ processId: 1 }), sampleServerStatistics({ processId: 2 })), undefined);
  });
});

describe('buildViewModel', () => {
  const data = () => ({
    host: host(),
    index: sampleIndexStatistics(),
    server: sampleServerStatistics(),
    details: sampleIndexDetails(),
  });

  it('describes the header', () => {
    const vm = buildViewModel(data(), options);
    assert.deepEqual(vm.status, { kind: 'ok', text: 'Idle' });
    assert.deepEqual(vm.facts, [
      '1 project', '2,653,112 files', 'index updated time(2026-11-04T22:42:13Z)', 'server 1.2.3', '.NET 10.0.0',
      'pid 4242', 'uptime 2 h 13 min',
    ]);
    assert.equal(vm.pauseOrResume, 'pause');
    assert.equal(vm.error, undefined);
  });

  it('shows "resume" when paused', () => {
    const vm = buildViewModel({ ...data(), index: sampleIndexStatistics({ status: 'paused' }) }, options);
    assert.equal(vm.pauseOrResume, 'resume');
    assert.equal(vm.actions.resume, true);
    assert.equal(vm.actions.pause, false);
  });

  it('describes the projects', () => {
    const details = {
      projects: [
        sampleProjectDetails('C:\\a'),
        sampleProjectDetails('C:\\b', { trigramFiltersEnabled: true, trigramFiltersBytes: 812 * 1024 * 1024 }),
      ],
    };
    const vm = buildViewModel({ ...data(), details }, options);
    assert.equal(vm.projects.length, 2);
    assert.deepEqual(vm.projects[0], {
      key: 'C:\\a',
      rootPath: 'C:\\a',
      files: '2,653,112',
      searchable: '2,176,934',
      textSize: '14.0 GB',
      trigramFiltersEnabled: false,
      trigramFilters: 'off',
      cache: '9.4 GB, saved',
      cacheTitle: '9,626 MB on disk (120 MB dead), saved time(2026-11-04T22:42:00Z) (index cache of the open projects)',
    });
    assert.equal(vm.projects[1].trigramFilters, '812.0 MB');
    assert.equal(vm.projects[1].trigramFiltersEnabled, true);
  });

  it('shows the state of the cache in the projects', () => {
    const cache = (state: object) => buildViewModel({
      ...data(),
      index: sampleIndexStatistics({ indexCache: { ...sampleIndexStatistics().indexCache!, ...state } }),
    }, options).projects[0].cache;
    assert.equal(cache({ dirty: true }), '9.4 GB, changed');
    assert.equal(cache({ saving: true }), '9.4 GB, saving\u2026');
    assert.equal(cache({ lastSavedUtc: undefined }), 'not saved');
    assert.equal(cache({ enabled: false }), 'disabled');
  });

  it('describes the memory with the lines of the status helpers', () => {
    const vm = buildViewModel(data(), options);
    assert.deepEqual(vm.memory.map(row => row.label), ['Memory', 'Indexed text', 'Arenas', 'Process', 'GC heap', 'Trigram filters']);
    assert.equal(vm.memory[0].value, '523.0 MB managed, 4,812.8 MB native');
    assert.match(vm.memory[2].value, /^586 \(9,376\.0 MB\): .* in use, 0\.0 MB dead, 33\.0 MB free$/);
  });

  it('describes the index cache', () => {
    const vm = buildViewModel(data(), options);
    assert.deepEqual(vm.indexCache.map(row => [row.label, row.value]), [
      ['State', '9,626 MB on disk (120 MB dead), saved time(2026-11-04T22:42:00Z)'],
      ['Directory', 'C:\\Users\\me\\AppData\\Local\\VsChromium\\IndexCache'],
      ['Projects', '1 open'],
      ['Last load', '2,227,630 files from the cache, 42,326 from disk'],
      ['Last write', '4.9 s, 120.0 MB written, 2,176,934 files (1,204 new)'],
    ]);
  });

  it('describes the operations, sorted by name', () => {
    const vm = buildViewModel(data(), options);
    assert.deepEqual(vm.operations.map(row => row.name), ['FileDatabaseUpdate', 'FileSystemScan', 'SearchCode']);
    assert.deepEqual(vm.operations[2], {
      key: 'SearchCode', name: 'SearchCode', count: '87', last: '250 ms', max: '570 ms', average: '250 ms', total: '21.8 s',
      recentMs: [250], recentTitle: 'Last duration: 250 ms',
    });
  });

  it('describes the server', () => {
    const previous = sampleServerStatistics({ uptimeMs: 1_000_000, cpuTotalMs: 1_000_000 });
    const current = sampleServerStatistics({ uptimeMs: 1_010_000, cpuTotalMs: 1_016_000 });
    const vm = buildViewModel({ ...data(), server: current, previousServer: previous }, options);
    assert.deepEqual(vm.server.map(row => row.label),
      ['Version', 'Runtime', 'Process', 'Uptime', 'CPU', 'Threads', 'Handles', 'Garbage collections']);
    assert.match(vm.server[4].value, /, 10\.0% now$/);
    assert.equal(vm.server[7].value, 'gen0 1,200, gen1 300, gen2 12, total pause 3.4 s');
  });

  it('shows nothing of a server that is not running', () => {
    const vm = buildViewModel({ host: host({ serverRunning: false }) }, options);
    assert.deepEqual(vm.facts, []);
    assert.deepEqual(vm.projects, []);
    assert.deepEqual(vm.memory, []);
    assert.deepEqual(vm.operations, []);
    assert.equal(vm.status.kind, 'stopped');
  });
});

describe('serverLogDirectory', () => {
  it('is in the local application data directory of .NET', () => {
    assert.equal(serverLogDirectory('win32', { LOCALAPPDATA: 'C:\\Users\\me\\AppData\\Local' }, 'C:\\Users\\me'),
      'C:\\Users\\me\\AppData\\Local\\VsChromium');
    assert.equal(serverLogDirectory('win32', {}, 'C:\\Users\\me'), 'C:\\Users\\me\\AppData\\Local\\VsChromium');
    assert.equal(serverLogDirectory('darwin', {}, '/Users/me'), '/Users/me/Library/Application Support/VsChromium');
    assert.equal(serverLogDirectory('linux', {}, '/home/me'), '/home/me/.local/share/VsChromium');
    assert.equal(serverLogDirectory('linux', { XDG_DATA_HOME: '/data' }, '/home/me'), '/data/VsChromium');
  });
});
