// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import type { IndexingStatus, ServerHistory } from '../../protocol/protocol.g';

// Charts of the server dashboard: the history of the server (merged from incremental
// "server/history" responses), derived series (CPU usage) and the geometry of the hand-written SVG
// charts (scales, axis ticks, paths, sparklines). Free of DOM dependencies (compiled into the page
// and unit tested with node).

/** The time window of the history charts (the history kept by the server). */
export const HistoryWindowMs = 2 * 60 * 60 * 1000;

/** The metric columns of `ServerHistory`. */
const numberColumns = [
  'gcHeapBytes',
  'gcTotalAllocatedBytes',
  'nativeAllocatedBytes',
  'workingSetBytes',
  'privateBytes',
  'cpuTotalMs',
  'fileCount',
  'searchableFileCount',
  'indexedTextBytes',
  'cacheSizeOnDiskBytes',
] as const;

/** The time of the last sample of `history`, if any. */
export function lastSampleTime(history: ServerHistory | undefined): number | undefined {
  const times = history?.timesUnixMs;
  return times && times.length > 0 ? times[times.length - 1] : undefined;
}

/**
 * Appends the samples of `incoming` (an incremental "server/history" response) to `existing`:
 * samples not newer than the last existing sample are ignored, the oldest samples are dropped
 * beyond the capacity of the server.
 */
export function mergeHistory(existing: ServerHistory | undefined, incoming: ServerHistory): ServerHistory {
  const last = lastSampleTime(existing);
  const start = last === undefined ? 0 : firstIndexAfter(incoming.timesUnixMs, last);
  const capacity = Math.max(1, incoming.capacity);
  const merge = <T>(a: T[] | undefined, b: T[]): T[] => {
    const all = (a ?? []).concat(b.slice(start));
    return all.length > capacity ? all.slice(all.length - capacity) : all;
  };
  const result: ServerHistory = {
    intervalMs: incoming.intervalMs,
    capacity: incoming.capacity,
    timesUnixMs: merge(existing?.timesUnixMs, incoming.timesUnixMs),
    status: merge(existing?.status, incoming.status),
  } as ServerHistory;
  for (const column of numberColumns) {
    result[column] = merge(existing?.[column], incoming[column]);
  }
  return result;
}

function firstIndexAfter(times: number[], time: number): number {
  let i = 0;
  while (i < times.length && times[i] <= time) {
    i++;
  }
  return i;
}

/**
 * The CPU usage of the server between consecutive samples, in percent of the machine (all the
 * processors): the slope of the CPU time. NaN for the first sample and when it can't be computed
 * (CPU time going back: another server process).
 */
export function cpuPercentSeries(times: number[], cpuTotalMs: number[], processorCount: number): number[] {
  return rateSeries(times, cpuTotalMs).map(rate => (isNaN(rate) ? NaN : Math.min(100, (rate * 100) / Math.max(1, processorCount))));
}

/** The slope of `values` per millisecond between consecutive samples (NaN when unknown). */
export function rateSeries(times: number[], values: number[]): number[] {
  return values.map((value, i) => {
    if (i === 0) {
      return NaN;
    }
    const elapsed = times[i] - times[i - 1];
    const delta = value - values[i - 1];
    return elapsed > 0 && delta >= 0 ? delta / elapsed : NaN;
  });
}

/** A "nice" maximum of a value axis (1, 2, 2.5 or 5 times a power of 10) and its ticks. */
export function niceScale(max: number, tickCount = 4): { max: number; ticks: number[] } {
  if (!(max > 0) || !isFinite(max)) {
    return { max: 1, ticks: [0, 1] };
  }
  const rough = max / tickCount;
  const power = Math.pow(10, Math.floor(Math.log10(rough)));
  const step = [1, 2, 2.5, 5, 10].map(m => m * power).find(s => s >= rough) ?? 10 * power;
  const niceMax = Math.ceil(max / step - 1e-9) * step;
  const ticks: number[] = [];
  for (let t = 0; t <= niceMax + step / 2; t += step) {
    ticks.push(Number(t.toPrecision(12)));
  }
  return { max: niceMax, ticks };
}

/** The ticks of the time axis of the history charts: every 30 minutes before `endMs` ("now"). */
export function timeTicks(endMs: number, windowMs = HistoryWindowMs): { time: number; label: string }[] {
  const ticks: { time: number; label: string }[] = [];
  const stepMs = 30 * 60 * 1000;
  for (let offset = windowMs; offset >= 0; offset -= stepMs) {
    const minutes = offset / 60_000;
    const label = offset === 0 ? 'now' : minutes % 60 === 0 ? `\u2212${minutes / 60} h` : `\u2212${minutes} min`;
    ticks.push({ time: endMs - offset, label });
  }
  return ticks;
}

/** The pixel area of a chart and its scales. */
export interface ChartFrame {
  /** Plot area, in pixels. */
  left: number;
  top: number;
  width: number;
  height: number;
  startMs: number;
  endMs: number;
  yMax: number;
}

export function xOf(frame: ChartFrame, time: number): number {
  return frame.left + ((time - frame.startMs) * frame.width) / Math.max(1, frame.endMs - frame.startMs);
}

export function yOf(frame: ChartFrame, value: number): number {
  const clamped = Math.max(0, Math.min(frame.yMax, value));
  return frame.top + frame.height - (clamped * frame.height) / frame.yMax;
}

/**
 * The SVG path ("d") of a series: a polyline of the samples in the time window, interrupted where
 * values are unknown (NaN) or samples are missing (gap of more than 3 intervals, e.g. the server
 * was suspended).
 */
export function linePath(frame: ChartFrame, times: number[], values: number[], intervalMs: number): string {
  const parts: string[] = [];
  let previousTime: number | undefined;
  for (let i = 0; i < times.length; i++) {
    const time = times[i];
    const value = values[i];
    if (time < frame.startMs || time > frame.endMs || value === undefined || isNaN(value)) {
      previousTime = undefined;
      continue;
    }
    const gap = previousTime === undefined || time - previousTime > 3 * intervalMs;
    parts.push(`${gap ? 'M' : 'L'}${round(xOf(frame, time))} ${round(yOf(frame, value))}`);
    previousTime = time;
  }
  return parts.join(' ');
}

function round(value: number): number {
  return Math.round(value * 10) / 10;
}

/** The maximum of the finite values of the series in the time window (0 if none). */
export function seriesMax(times: number[], series: number[][], startMs: number): number {
  let max = 0;
  for (const values of series) {
    values.forEach((value, i) => {
      if (times[i] >= startMs && isFinite(value) && value > max) {
        max = value;
      }
    });
  }
  return max;
}

/** A run of samples with the same status (status timeline). */
export interface StatusSegment {
  status: IndexingStatus;
  startMs: number;
  endMs: number;
}

/** The runs of the same status, each sample covering the interval ending at its time. */
export function statusSegments(times: number[], statuses: IndexingStatus[], intervalMs: number, startMs: number): StatusSegment[] {
  const segments: StatusSegment[] = [];
  for (let i = 0; i < times.length; i++) {
    const end = times[i];
    if (end < startMs) {
      continue;
    }
    const begin = Math.max(startMs, i > 0 && end - times[i - 1] <= 3 * intervalMs ? times[i - 1] : end - intervalMs);
    const last = segments[segments.length - 1];
    if (last && last.status === statuses[i] && last.endMs === begin) {
      last.endMs = end;
    } else {
      segments.push({ status: statuses[i], startMs: begin, endMs: end });
    }
  }
  return segments;
}

/** A bar of a sparkline. */
export interface SparkBar {
  x: number;
  y: number;
  width: number;
  height: number;
  value: number;
  last: boolean;
}

/**
 * The bars of a sparkline of the last durations of an operation (oldest first), scaled to the
 * maximum, in a `width` x `height` box with `slots` bar positions (aligned to the right: the last
 * value is at the right end).
 */
export function sparkBars(values: number[], width: number, height: number, slots = 20): SparkBar[] {
  const max = Math.max(0, ...values.filter(v => isFinite(v)));
  const slotWidth = width / slots;
  const barWidth = Math.max(1, slotWidth - 1);
  const shown = values.slice(-slots);
  const first = slots - shown.length;
  return shown.map((value, i) => {
    const h = max > 0 ? Math.max(1, (value * height) / max) : 1;
    return {
      x: round((first + i) * slotWidth),
      y: round(height - h),
      width: round(barWidth),
      height: round(h),
      value,
      last: i === shown.length - 1,
    };
  });
}

/** The index of the sample nearest to `time` (hover of a chart), or -1. */
export function nearestSample(times: number[], time: number): number {
  let best = -1;
  let bestDistance = Infinity;
  for (let i = 0; i < times.length; i++) {
    const distance = Math.abs(times[i] - time);
    if (distance < bestDistance) {
      best = i;
      bestDistance = distance;
    }
  }
  return best;
}
