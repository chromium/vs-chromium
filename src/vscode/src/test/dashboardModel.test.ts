// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { IndexingStatus } from '../protocol/protocol.g';
import type { HostState } from '../ui/dashboard/dashboardMessages';
import {
  buildViewModel,
  describeCacheVerification,
  describeProjectOperations,
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
  it('requests the statistics, the history and the project details every 5 refreshes', () => {
    const plans = Array.from({ length: 16 }, (_, i) => planRefresh(i));
    assert.deepEqual(plans.map((p, i) => p.statistics ? i : -1).filter(i => i >= 0), [0, 5, 10, 15]);
    assert.deepEqual(plans.map((p, i) => p.details ? i : -1).filter(i => i >= 0), [0, 5, 10, 15]);
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
        sampleProjectDetails('C:\\b', {
          trigramFiltersEnabled: true,
          memory: { ...sampleProjectDetails('C:\\b').memory, trigramFiltersBytes: 812 * 1024 * 1024 },
        }),
      ],
    };
    const vm = buildViewModel({ ...data(), details }, options);
    assert.equal(vm.projects.length, 2);
    const a = vm.projects[0];
    assert.equal(a.key, 'C:\\a');
    assert.equal(a.summary, '2,653,112 files \u00b7 native 4.6 GB \u00b7 cache 9.4 GB');
    assert.equal(a.defaultExpanded, true);
    assert.deepEqual(a.index.map(kv => kv.value), [
      '2,653,112 files \u00b7 2,176,934 searchable \u00b7 14.0 GB of text',
      'time(2026-11-04T22:42:13Z)',
    ]);
    assert.deepEqual(a.memory.map(kv => [kv.label, kv.value]), [
      ['Native', '4.6 GB (290 arenas, 0 B dead, 30.0 MB free; 70.0 MB in files > 512 KB)'],
      ['Mapped', '9.3 GB from the cache'],
      ['Trigram filters', '0 B'],
    ]);
    assert.deepEqual(a.cache, {
      enabled: true,
      writable: true,
      directory: 'C:\\Users\\me\\AppData\\Local\\VsChromium\\Cache\\3f2a0b1c2d3e4f50',
      state: '9.4 GB on disk (120.0 MB dead) \u00b7 saved time(2026-11-04T22:42:13Z)',
    });
    assert.equal(a.trigramFilters, 'off');
    assert.equal(vm.projects[1].trigramFilters, '812.0 MB');
    assert.equal(vm.projects[1].trigramFiltersEnabled, true);
    assert.equal(a.externalLinks, undefined, 'No symbolic link');
  });

  it('warns about the symbolic links to targets outside of the project', () => {
    const links = {
      fileLinkCount: 44_520, directoryLinkCount: 838, externalTargetLinkCount: 1_204, externalTargetDirectoryCount: 428,
      networkTargetLinkCount: 2, externalTargetsWatched: false, watcherCount: 0,
    };
    const details = {
      projects: [
        sampleProjectDetails('C:\\a', { symbolicLinks: links }),
        sampleProjectDetails('C:\\b', { symbolicLinks: { ...links, externalTargetsWatched: true, watcherCount: 428 } }),
        sampleProjectDetails('C:\\c', { symbolicLinks: { ...links, externalTargetLinkCount: 0, externalTargetDirectoryCount: 0 } }),
      ],
    };
    const vm = buildViewModel({ ...data(), details }, options);
    const [a, b, c] = vm.projects;
    assert.equal(a.index.find(kv => kv.key === 'symlinks')?.value,
      '44,520 file links \u00b7 838 directory links \u00b7 2 links to network volumes (not watched)');
    assert.equal(a.externalLinks?.watched, false);
    assert.equal(a.externalLinks?.warning,
      '1,204 symbolic links point outside the project (428 directories): changes to their targets are only picked up when the server restarts.');
    assert.equal(a.externalLinks?.state, '');
    assert.match(a.externalLinks!.title, /one directory watcher per target directory \(428 for this project, ~8 KB of kernel memory each/);
    // Watched: no warning, the count of watchers.
    assert.equal(b.externalLinks?.watched, true);
    assert.equal(b.externalLinks?.warning, undefined);
    assert.equal(b.externalLinks?.state, '\u00b7 428 watchers');
    // No target outside of the project: no checkbox.
    assert.equal(c.externalLinks, undefined);
    const single = buildViewModel({ ...data(), details: { projects: [sampleProjectDetails('C:\\d', {
      symbolicLinks: { ...links, externalTargetLinkCount: 1, externalTargetDirectoryCount: 1 },
    })] } }, options).projects[0];
    assert.equal(single.externalLinks?.warning,
      '1 symbolic link points outside the project (1 directory): changes to their targets are only picked up when the server restarts.');
  });

  it('collapses the projects by default when there are more than 2', () => {
    const details = { projects: ['C:\\a', 'C:\\b', 'C:\\c'].map(p => sampleProjectDetails(p)) };
    assert.deepEqual(buildViewModel({ ...data(), details }, options).projects.map(p => p.defaultExpanded), [false, false, false]);
  });

  it('shows the state of the cache of the projects', () => {
    const cache = (state: object) => buildViewModel({
      ...data(),
      details: { projects: [sampleProjectDetails('C:\\a', { cache: { ...sampleProjectDetails('C:\\a').cache, ...state } })] },
    }, options).projects[0];
    assert.equal(cache({ dirty: true }).cache.state, '9.4 GB on disk (120.0 MB dead) \u00b7 saved time(2026-11-04T22:42:13Z) \u00b7 changed since');
    assert.equal(cache({ saving: true, deadBytes: 0 }).cache.state, '9.4 GB on disk \u00b7 saved time(2026-11-04T22:42:13Z) \u00b7 saving\u2026');
    assert.equal(cache({ lastSavedUtc: undefined }).cache.state, 'not saved yet');
    assert.equal(cache({ enabled: false }).cache.state, 'disabled');
    assert.equal(cache({ enabled: false }).summary, '2,653,112 files \u00b7 native 4.6 GB \u00b7 cache disabled');
    assert.equal(cache({ writable: false }).cache.writable, false);
  });

  it('describes the operations of the projects (no search times)', () => {
    const vm = buildViewModel(data(), options);
    assert.deepEqual(vm.projects[0].operations, [
      { key: 'initialLoad', name: 'Initial load', count: '1', min: '1 min 6 s', avg: '1 min 6 s', max: '1 min 6 s', last: '1 min 6 s', note: 'scan 15.0 s, from the cache: 4.9 s, verified from the change journal: 312 directories listed again in 420 ms' },
      { key: 'incrementalUpdates', name: 'Incremental updates', count: '48', min: '40 ms', avg: '300 ms', max: '2.1 s', last: '120 ms', note: '' },
    ]);
    assert.equal(describeCacheVerification({
      method: 'fullScan', fallbackReason: 'the change journal is not active on "D:\\"', durationMs: 900, journalRecordCount: 0,
      listedDirectoryCount: 0, scannedDirectoryCount: 0, symLinkDirectoryCount: 0, symLinkFileCount: 0,
    }), 'verified by a full scan (the change journal is not active on "D:\\")');
    assert.equal(describeCacheVerification({
      method: 'fullScan', fallbackReason: 'the change journal wrapped', durationMs: 900, journalRecordCount: 0,
      listedDirectoryCount: 0, scannedDirectoryCount: 0, symLinkDirectoryCount: 0, symLinkFileCount: 0,
      checkpointUsn: 1234, checkpointTimeUtc: '2026-11-05T22:40:10Z', oldestRecordTimeUtc: '2026-11-06T03:12:45.123Z',
      journalMaximumSizeBytes: 33_554_432, hint: 'Enlarge it: fsutil usn createjournal m=1073741824 a=134217728 C:',
    }), 'verified by a full scan (the change journal wrapped); journal since 2026-11-06 03:12 UTC, checkpoint of 2026-11-05 22:40 UTC. ' +
      'Enlarge it: fsutil usn createjournal m=1073741824 a=134217728 C:');
    const none = describeProjectOperations(sampleProjectDetails('C:\\a', {
      operations: {
        initialLoad: { count: 0, minMs: 0, avgMs: 0, maxMs: 0, lastMs: 0, scanMs: 0, fromCache: false },
        incrementalUpdates: { count: 0, minMs: 0, avgMs: 0, maxMs: 0, lastMs: 0 },
      },
    }));
    assert.deepEqual(none.map(op => [op.count, op.min, op.last, op.note]), [['0', '\u2014', '\u2014', ''], ['0', '\u2014', '\u2014', '']]);
  });

  it('describes the memory of the process', () => {
    const vm = buildViewModel(data(), options);
    assert.deepEqual(vm.memory.map(row => [row.label, row.value]), [
      ['Managed', '523.0 MB (48.0 GB allocated since the start)'],
      ['Native', '4.7 GB (file contents of all the projects)'],
      ['Mapped', '9.3 GB from the index caches'],
      ['Process', 'working set 6.1 GB (peak 7.2 GB), private 5.4 GB'],
    ]);
  });

  it('describes the index cache of the process', () => {
    const vm = buildViewModel(data(), options);
    assert.deepEqual(vm.indexCache.map(row => [row.label, row.value]), [
      ['Directory', 'C:\\Users\\me\\AppData\\Local\\VsChromium\\IndexCache'],
      ['Total', '9,626 MB on disk (120 MB dead), saved time(2026-11-04T22:42:00Z) \u00b7 1 project'],
    ]);
  });

  it('describes the operation timings, without the search times, sorted by name', () => {
    const server = sampleServerStatistics({
      operationTimings: [
        ...sampleServerStatistics().operationTimings,
        { name: 'Request:search/code', count: 3, lastMs: 1, totalMs: 3, maxMs: 1, recentMs: [1], recentEndUnixMs: [1] },
        { name: 'Request:index/status', count: 3, lastMs: 1, totalMs: 3, maxMs: 1, recentMs: [1], recentEndUnixMs: [1] },
      ],
    });
    const vm = buildViewModel({ ...data(), server }, options);
    assert.deepEqual(vm.operations.map(row => row.name), ['FileDatabaseUpdate', 'FileSystemScan', 'Request:index/status']);
    assert.deepEqual(vm.operations[1], {
      key: 'FileSystemScan', name: 'FileSystemScan', count: '3', last: '15.0 s', max: '16.8 s', average: '16.0 s', total: '48.0 s',
      recentMs: [16_800, 16_200, 15_000], recentTitle: 'Last 3 durations (oldest first): 16.8 s, 16.2 s, 15.0 s',
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


describe('diskUsage view model', () => {
  const MB = 1024 * 1024;

  it('builds breadcrumbs, stacked bars, sizes, and ignore button states', () => {
    const vm = buildViewModel({
      host: host(),
      diskUsage: {
        projectPath: 'C:\\repo',
        relativePath: 'src/core',
        loading: false,
        sizes: {
          projectPath: 'C:\\repo',
          relativePath: 'src/core',
          projectFile: 'C:\\repo\\vs-chromium-project.txt',
          snapshotVersion: 3,
          computeMs: 1.5,
          totals: {
            name: 'core',
            isDirectory: true,
            excluded: false,
            measureState: 'done',
            totalBytes: 64 * MB,
            searchableBytes: 48 * MB,
            notSearchableBytes: 16 * MB,
            fileCount: 120,
            searchableFileCount: 95,
            pattern: '/src/core/',
          },
          children: [
            {
              name: 'generated',
              isDirectory: true,
              excluded: false,
              measureState: 'done',
              totalBytes: 64 * MB,
              searchableBytes: 48 * MB,
              notSearchableBytes: 16 * MB,
              fileCount: 100,
              searchableFileCount: 80,
              pattern: '/src/core/generated/',
            },
            {
              name: 'assets.pak',
              isDirectory: false,
              excluded: false,
              measureState: 'done',
              totalBytes: 16 * MB,
              searchableBytes: 0,
              notSearchableBytes: 16 * MB,
              fileCount: 1,
              searchableFileCount: 0,
              pattern: '/src/core/assets.pak',
            },
            {
              name: 'out',
              isDirectory: true,
              excluded: true,
              measureState: 'notMeasured',
              totalBytes: 0,
              searchableBytes: 0,
              notSearchableBytes: 0,
              fileCount: 0,
              searchableFileCount: 0,
              pattern: '/src/core/out/',
            },
            {
              name: 'cache',
              isDirectory: true,
              excluded: true,
              measureState: 'done',
              totalBytes: 32 * MB,
              searchableBytes: 0,
              notSearchableBytes: 32 * MB,
              fileCount: 400,
              searchableFileCount: 0,
              pattern: '/src/core/cache/',
            },
          ],
        },
      },
    }, options);

    assert.equal(vm.view, 'diskUsage');
    const du = vm.diskUsage!;
    assert.equal(du.parentRelativePath, 'src');
    assert.deepEqual(du.breadcrumbs.map(b => [b.label, b.relativePath, b.current]), [
      ['C:\\repo', '', false],
      ['src', 'src', false],
      ['core', 'src/core', true],
    ]);
    assert.equal(du.summary, 'Indexed: 48.0 MB searchable / 64.0 MB total \u00b7 120 files (95 searchable)');
    assert.equal(du.currentDirectoryPattern, '/src/core/');
    assert.equal(du.canExcludeCurrentExplorer, true);
    assert.equal(du.canExcludeCurrentSearchable, true);

    const [gen, pak, out, cache] = du.rows;
    assert.equal(gen.drillable, true);
    assert.equal(gen.sizeText, '48.0 MB / 64.0 MB');
    assert.equal(gen.searchableBarPercent, 75);
    assert.equal(gen.notSearchableBarPercent, 25);
    assert.equal(gen.canAddExplorerIgnore, true);
    assert.equal(gen.canAddSearchableIgnore, true);

    assert.equal(pak.drillable, false);
    assert.equal(pak.sizeText, '0 B / 16.0 MB');
    assert.equal(pak.searchableBarPercent, 0);
    assert.equal(pak.notSearchableBarPercent, 25);
    assert.equal(pak.canAddExplorerIgnore, true);
    assert.equal(pak.canAddSearchableIgnore, false, 'already 0 searchable bytes');

    assert.equal(out.drillable, false);
    assert.equal(out.sizeText, 'excluded \u2014 size not measured');
    assert.equal(out.measureAction, 'measure');
    assert.equal(out.canCopyPattern, true);
    assert.equal(out.canAddExplorerIgnore, false);

    assert.equal(cache.sizeText, '0 B / 32.0 MB');
    assert.equal(cache.measureAction, 'remeasure');
    assert.equal(cache.notSearchableBarPercent, 50);
  });

  it('disables adding ignore rules when the project has no editable vs-chromium-project.txt', () => {
    const vm = buildViewModel({
      host: host(),
      diskUsage: {
        projectPath: 'C:\\chromium\\src',
        relativePath: '',
        loading: false,
        sizes: {
          projectPath: 'C:\\chromium\\src',
          relativePath: '',
          projectFile: undefined,
          snapshotVersion: 1,
          computeMs: 5,
          totals: {
            name: 'src',
            isDirectory: true,
            excluded: false,
            measureState: 'done',
            totalBytes: 1024,
            searchableBytes: 1024,
            notSearchableBytes: 0,
            fileCount: 1,
            searchableFileCount: 1,
          },
          children: [
            {
              name: 'third_party',
              isDirectory: true,
              excluded: false,
              measureState: 'done',
              totalBytes: 1024,
              searchableBytes: 1024,
              notSearchableBytes: 0,
              fileCount: 1,
              searchableFileCount: 1,
              pattern: '/third_party/',
            },
          ],
        },
      },
    }, options);

    const du = vm.diskUsage!;
    assert.ok(du.readOnlyReason);
    assert.equal(du.rows[0].canCopyPattern, true);
    assert.equal(du.rows[0].canAddExplorerIgnore, false);
    assert.equal(du.rows[0].canAddSearchableIgnore, false);
  });
});
