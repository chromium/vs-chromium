// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  ChartFrame,
  cpuPercentSeries,
  HistoryWindowMs,
  linePath,
  mergeHistory,
  nearestSample,
  niceScale,
  rateSeries,
  sparkBars,
  statusSegments,
  timeTicks,
} from '../ui/dashboard/dashboardCharts';
import { buildViewModel, bytesScale, describeCacheSettings, percentScale } from '../ui/dashboard/dashboardModel';
import type { HostState } from '../ui/dashboard/dashboardMessages';
import { historyOf, sampleIndexStatus, sampleServerStatistics, syntheticHistory } from './dashboardSampleData';

const MB = 1024 * 1024;
const GB = 1024 * MB;

const host = (state: Partial<HostState> = {}): HostState => ({
  hostName: 'Test',
  serverRunning: true,
  trigramFiltersSetting: false,
  ...state,
});

describe('mergeHistory', () => {
  it('appends the new samples', () => {
    const first = historyOf([{ time: 10_000 }, { time: 20_000 }]);
    const merged = mergeHistory(first, historyOf([{ time: 20_000, gcHeapBytes: 1 }, { time: 30_000, gcHeapBytes: 2 }]));
    assert.deepEqual(merged.timesUnixMs, [10_000, 20_000, 30_000]);
    assert.deepEqual(merged.gcHeapBytes, [100 * MB, 100 * MB, 2], 'samples already received are ignored');
    assert.equal(merged.status.length, 3);
    assert.equal(merged.cacheSizeOnDiskBytes.length, 3);
  });

  it('starts from the first response', () => {
    const merged = mergeHistory(undefined, historyOf([{ time: 10_000 }]));
    assert.deepEqual(merged.timesUnixMs, [10_000]);
  });

  it('keeps at most the capacity of the server', () => {
    const existing = { ...historyOf([{ time: 1 }, { time: 2 }, { time: 3 }]), capacity: 4 };
    const merged = mergeHistory(existing, { ...historyOf([{ time: 4 }, { time: 5 }, { time: 6 }]), capacity: 4 });
    assert.deepEqual(merged.timesUnixMs, [3, 4, 5, 6]);
    assert.equal(merged.cpuTotalMs.length, 4);
  });
});

describe('derived series', () => {
  it('computes the CPU usage from the slope of the CPU time', () => {
    // 16 processors, 10 s between samples: 40 s of CPU = 25% of the machine.
    const cpu = cpuPercentSeries([0, 10_000, 20_000, 30_000], [0, 40_000, 40_000, 200_000], 16);
    assert.ok(isNaN(cpu[0]));
    assert.equal(cpu[1], 25);
    assert.equal(cpu[2], 0);
    assert.equal(cpu[3], 100, 'clamped');
  });

  it('ignores negative deltas and missing intervals', () => {
    const rate = rateSeries([0, 10, 10, 30], [0, 5, 6, 1]);
    assert.ok(isNaN(rate[0]));
    assert.equal(rate[1], 0.5);
    assert.ok(isNaN(rate[2]), 'same time');
    assert.ok(isNaN(rate[3]), 'counter going back');
  });
});

describe('scales', () => {
  it('rounds the maximum up to a nice value', () => {
    assert.deepEqual(niceScale(7.3), { max: 8, ticks: [0, 2, 4, 6, 8] });
    assert.deepEqual(niceScale(100), { max: 100, ticks: [0, 25, 50, 75, 100] });
    assert.deepEqual(niceScale(0.9), { max: 1, ticks: [0, 0.25, 0.5, 0.75, 1] });
    assert.deepEqual(niceScale(0), { max: 1, ticks: [0, 1] });
  });

  it('labels bytes in MB or GB', () => {
    const gb = bytesScale(6.4 * GB);
    assert.equal(gb.max, 8 * GB);
    assert.deepEqual(gb.ticks.map(t => t.label), ['0', '2 GB', '4 GB', '6 GB', '8 GB']);
    assert.deepEqual(bytesScale(300 * MB).ticks.map(t => t.label), ['0', '100 MB', '200 MB', '300 MB']);
  });

  it('labels percentages, at least up to 10%', () => {
    assert.deepEqual(percentScale(3).ticks.map(t => t.label), ['0%', '2.5%', '5%', '7.5%', '10%']);
    assert.equal(percentScale(250).max, 100);
  });

  it('labels the time axis of the last 2 hours', () => {
    const ticks = timeTicks(HistoryWindowMs);
    assert.deepEqual(ticks.map(t => t.label), ['\u22122 h', '\u221290 min', '\u22121 h', '\u221230 min', 'now']);
    assert.equal(ticks[0].time, 0);
    assert.equal(ticks[4].time, HistoryWindowMs);
  });
});

describe('chart geometry', () => {
  const frame: ChartFrame = { left: 10, top: 0, width: 100, height: 50, startMs: 0, endMs: 100, yMax: 10 };

  it('draws the samples of the window, with gaps', () => {
    assert.equal(linePath(frame, [0, 10, 20], [0, 5, 10], 10), 'M10 50 L20 25 L30 0');
    assert.equal(linePath(frame, [0, 10, 20, 30], [0, NaN, 5, 5], 10), 'M10 50 M30 25 L40 25', 'unknown values');
    assert.equal(linePath(frame, [0, 10, 90], [1, 1, 1], 10), 'M10 45 L20 45 M100 45', 'missing samples');
    assert.equal(linePath(frame, [-10, 50], [1, 20], 10), 'M60 0', 'outside of the window, clamped');
  });

  it('merges the samples of the same status', () => {
    const segments = statusSegments([10, 20, 30, 40, 50], ['busy', 'busy', 'idle', 'idle', 'paused'], 10, 0);
    assert.deepEqual(segments, [
      { status: 'busy', startMs: 0, endMs: 20 },
      { status: 'idle', startMs: 20, endMs: 40 },
      { status: 'paused', startMs: 40, endMs: 50 },
    ]);
  });

  it('scales the sparkline bars to the maximum, last value at the right', () => {
    const bars = sparkBars([10, 20, 5], 20, 10, 4);
    assert.deepEqual(bars.map(b => [b.x, b.height, b.last]), [[5, 5, false], [10, 10, false], [15, 2.5, true]]);
    assert.equal(bars[1].y, 0);
    assert.deepEqual(sparkBars([], 20, 10), []);
  });

  it('finds the sample under the mouse', () => {
    assert.equal(nearestSample([0, 10, 20], 13), 1);
    assert.equal(nearestSample([], 13), -1);
  });
});

describe('history charts of the view model', () => {
  const endMs = 1_780_000_000_000;
  const data = () => ({
    host: host(),
    status: sampleIndexStatus(),
    server: sampleServerStatistics(),
    history: syntheticHistory(endMs),
  });

  it('draws the memory, CPU and cache charts over the last 2 hours', () => {
    const vm = buildViewModel(data());
    assert.equal(vm.memoryChart?.endMs, endMs);
    assert.equal(vm.memoryChart?.startMs, endMs - HistoryWindowMs);
    assert.deepEqual(vm.memoryChart?.series.map(s => s.label), ['GC heap', 'Native', 'Working set', 'Private bytes']);
    assert.equal(vm.memoryChart?.unit, 'bytes');
    assert.equal(vm.cpuChart?.unit, 'percent');
    assert.ok(vm.cpuChart!.series[0].values.slice(1).every(v => v >= 0 && v <= 100));
    assert.deepEqual(vm.cacheChart?.series.map(s => s.label), ['Size on disk']);
    assert.ok(vm.statusTimeline!.segments.length >= 5);
    assert.equal(vm.statusTimeline!.segments[0].status, 'busy');
  });

  it('changes the version of the charts only when samples arrive', () => {
    const a = buildViewModel(data());
    const b = buildViewModel(data());
    assert.equal(a.memoryChart?.version, b.memoryChart?.version);
    const c = buildViewModel({ ...data(), history: syntheticHistory(endMs + 10_000) });
    assert.notEqual(a.memoryChart?.version, c.memoryChart?.version);
  });

  it('has no charts without history, no CPU chart without the processor count, no cache chart without cache', () => {
    assert.equal(buildViewModel({ ...data(), history: undefined }).memoryChart, undefined);
    assert.equal(buildViewModel({ ...data(), server: undefined }).cpuChart, undefined);
    const noCache = buildViewModel({ ...data(), status: sampleIndexStatus({ indexCache: { ...sampleIndexStatus().indexCache!, enabled: false } }) });
    assert.equal(noCache.cacheChart, undefined);
    assert.equal(buildViewModel({ ...data(), host: host({ serverRunning: false }) }).memoryChart, undefined);
  });

  it('adds the sparkline of the last durations of the operations', () => {
    const vm = buildViewModel(data());
    const scan = vm.operations.find(o => o.name === 'FileSystemScan')!;
    assert.deepEqual(scan.recentMs, [16_800, 16_200, 15_000]);
    assert.equal(scan.recentTitle, 'Last 3 durations (oldest first): 16.8 s, 16.2 s, 15.0 s');
  });
});

describe('describeCacheSettings', () => {
  const data = (cacheSettings: { enabled: boolean; directory: string }, serverDirectory = 'C:\\Users\\me\\AppData\\Local\\VsChromium\\IndexCache') => ({
    host: host({ cacheSettings }),
    status: sampleIndexStatus(),
    server: sampleServerStatistics({ indexCache: { ...sampleServerStatistics().indexCache!, cacheDirectory: serverDirectory } }),
  });

  it('shows the settings of the host', () => {
    assert.deepEqual(describeCacheSettings(data({ enabled: true, directory: '' })), { enabled: true, directory: 'default', restartNeeded: false });
    assert.equal(describeCacheSettings({ host: host() }), undefined);
  });

  it('tells when the settings differ from the running server', () => {
    assert.equal(describeCacheSettings(data({ enabled: false, directory: '' }))?.restartNeeded, true);
    assert.equal(describeCacheSettings(data({ enabled: true, directory: 'D:\\Cache' }))?.restartNeeded, true);
    assert.equal(describeCacheSettings(data({ enabled: true, directory: 'c:\\users\\me\\appdata\\local\\vschromium\\indexcache\\' }))?.restartNeeded, false);
  });
});
