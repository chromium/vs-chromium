// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import type {
  IndexDetails,
  IndexStatistics,
  IndexStatus,
  IndexingStatus,
  OperationTiming,
  ProgressParams,
  ProjectDetails,
  ServerHistory,
  ServerStatistics,
} from '../../protocol/protocol.g';
import { describeIndexCache } from '../indexCacheStatus';
import {
  cpuPercentSeries,
  HistoryWindowMs,
  lastSampleTime,
  niceScale,
  seriesMax,
  statusSegments,
  StatusSegment,
  timeTicks,
} from './dashboardCharts';
import type { HostState } from './dashboardMessages';

// View model of the server dashboard page: what the page displays (texts, rows, enabled actions,
// charts), computed from the protocol data. Free of DOM and host dependencies (compiled into the
// page and unit tested with node).

/** Interval of the refreshes while the page is visible. */
export const RefreshIntervalMs = 2000;
/**
 * "index/statistics" (arena statistics: walks the index), "server/statistics" (process, GC,
 * operations; walks the index) and "server/history" (incremental) are requested every N refreshes.
 */
export const StatisticsEveryRefreshes = 5;
/**
 * "index/details" (the details of all the projects: files, memory, cache, operations; walks the
 * directories of the projects) is requested every N refreshes.
 */
export const DetailsEveryRefreshes = 5;

/** What a refresh requests ("index/status", cheap, is always requested: status, counts, cache). */
export interface RefreshPlan {
  /** "index/statistics", "server/statistics" and "server/history". */
  statistics: boolean;
  details: boolean;
}

/**
 * The requests of the refresh number `refresh` (0 for the first one): the cheap index status at
 * each refresh, the statistics, the history and the project details every 10 s, plus
 * those forced by events (e.g. a load of the index changes the project details).
 */
export function planRefresh(refresh: number, forced: Partial<RefreshPlan> = {}): RefreshPlan {
  return {
    statistics: refresh % StatisticsEveryRefreshes === 0 || forced.statistics === true,
    details: refresh % DetailsEveryRefreshes === 0 || forced.details === true,
  };
}

/** The data displayed by the page. */
export interface DashboardData {
  host?: HostState;
  /** The last "index/status" (or the status part of the last "index/statistics"). */
  status?: IndexStatus;
  /** The last "index/statistics" (arena statistics), updated with the last status. */
  index?: IndexStatistics;
  server?: ServerStatistics;
  /** The server statistics before `server` (CPU usage between the two). */
  previousServer?: ServerStatistics;
  /** The history of the server (merged "server/history" responses). */
  history?: ServerHistory;
  details?: IndexDetails;
  /** Last progress notification of the current indexing operation. */
  progress?: ProgressParams;
  /** Error of the last refresh. */
  error?: string;
}

export type ActionId =
  | 'pause'
  | 'resume'
  | 'rebuild'
  | 'restartServer'
  | 'showOutput'
  | 'openLogDirectory'
  | 'toggleTrigramFilters'
  | 'clearCache'
  | 'openCacheSettings';

export type StatusKind = 'ok' | 'busy' | 'paused' | 'stopped' | 'unknown';

export interface KeyValue {
  key: string;
  label: string;
  value: string;
  title?: string;
}

/** A row of the operations of a project. */
export interface ProjectOperationRow {
  key: string;
  name: string;
  count: string;
  min: string;
  avg: string;
  max: string;
  last: string;
  /** Details of the last operation (e.g. the scan time, the load from the cache). */
  note: string;
}

/** The cache of a project. */
export interface ProjectCacheView {
  enabled: boolean;
  /** False if the cache is written by another server (used read-only). */
  writable: boolean;
  directory?: string;
  /** "9.4 GB on disk (120 MB dead) · saved 2:42 PM · changed". */
  state: string;
}

/** The block of a project (collapsible). */
export interface ProjectBlock {
  key: string;
  rootPath: string;
  /** One line summary (displayed when the block is collapsed). */
  summary: string;
  /** Expanded unless there are many projects (the user can toggle it). */
  defaultExpanded: boolean;
  index: KeyValue[];
  memory: KeyValue[];
  cache: ProjectCacheView;
  trigramFiltersEnabled: boolean;
  /** "812.0 MB" (memory of the filters) or "off". */
  trigramFilters: string;
  operations: ProjectOperationRow[];
}

/** Projects expanded by default up to this count. */
export const ExpandedProjectsMax = 2;

export interface OperationRow {
  key: string;
  name: string;
  count: string;
  last: string;
  max: string;
  average: string;
  total: string;
  /** The last durations (oldest first), for the sparkline. */
  recentMs: number[];
  /** Tooltip of the sparkline. */
  recentTitle: string;
}

/** A series of a history chart. */
export interface ChartSeries {
  key: string;
  label: string;
  /** Index of the color of the series (CSS class "series-N": the VS Code chart colors). */
  color: number;
  values: number[];
}

/** A line chart over the history of the server (time axis: the last 2 hours, "now" at the right). */
export interface HistoryChart {
  key: string;
  /** Changes when the data of the chart changes (the page redraws the chart only then). */
  version: string;
  unit: 'bytes' | 'percent';
  times: number[];
  intervalMs: number;
  startMs: number;
  endMs: number;
  yMax: number;
  yTicks: { value: number; label: string }[];
  xTicks: { time: number; label: string }[];
  series: ChartSeries[];
}

/** The status timeline under the memory chart. */
export interface StatusTimeline {
  version: string;
  startMs: number;
  endMs: number;
  segments: StatusSegment[];
}

/** The cache settings of the host (e.g. VS Code settings), applied when the server (re)starts. */
export interface CacheSettingsView {
  enabled: boolean;
  /** The directory, "default" if not set. */
  directory: string;
  /** The settings differ from the cache of the running server. */
  restartNeeded: boolean;
}

export interface DashboardViewModel {
  status: { kind: StatusKind; text: string };
  /** Facts of the header line (index update time, server version, runtime, pid, uptime...). */
  facts: string[];
  actions: Record<ActionId, boolean>;
  /** The pause/resume button shows "Resume" when indexing is paused. */
  pauseOrResume: 'pause' | 'resume';
  error?: string;
  trigramFiltersSetting: boolean;
  projects: ProjectBlock[];
  /** Memory of the whole process (process section). */
  memory: KeyValue[];
  /** The index cache of the whole process: directory, total size (process section). */
  indexCache: KeyValue[];
  cacheSettings?: CacheSettingsView;
  operations: OperationRow[];
  server: KeyValue[];
  memoryChart?: HistoryChart;
  cpuChart?: HistoryChart;
  cacheChart?: HistoryChart;
  statusTimeline?: StatusTimeline;
}

export interface FormatOptions {
  /** Formats a UTC time (protocol format) for display. */
  formatTime: (utc: string) => string;
}

const defaultFormatOptions: FormatOptions = {
  formatTime: utc => new Date(utc).toLocaleTimeString(),
};

const KB = 1024;
const MB = KB * 1024;
const GB = MB * 1024;

/** "512 B", "1.5 KB", "523.4 MB", "14.0 GB". */
export function formatBytes(bytes: number): string {
  const format = (value: number, unit: string) =>
    `${value.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} ${unit}`;
  if (bytes >= GB) {
    return format(bytes / GB, 'GB');
  }
  if (bytes >= MB) {
    return format(bytes / MB, 'MB');
  }
  if (bytes >= KB) {
    return format(bytes / KB, 'KB');
  }
  return `${Math.round(bytes)} B`;
}

/** "2,653,112". */
export function formatCount(count: number): string {
  return count.toLocaleString('en-US');
}

/** "250 ms", "4.9 s", "2 min 13 s", "2 h 13 min". */
export function formatDuration(ms: number): string {
  if (ms < 1000) {
    return `${Math.round(ms)} ms`;
  }
  if (ms < 60_000) {
    return `${(ms / 1000).toFixed(1)} s`;
  }
  const totalSeconds = Math.floor(ms / 1000);
  if (totalSeconds < 3600) {
    return `${Math.floor(totalSeconds / 60)} min ${totalSeconds % 60} s`;
  }
  return `${Math.floor(totalSeconds / 3600)} h ${Math.floor((totalSeconds % 3600) / 60)} min`;
}

/** True for a time set by the server (not the default value of a missing time). */
function isValidTime(utc: string | undefined): utc is string {
  if (!utc) {
    return false;
  }
  const time = Date.parse(utc);
  return !isNaN(time) && new Date(time).getUTCFullYear() > 2000;
}

/** The current state of the index: the last status, or the last statistics. */
function currentStatus(data: DashboardData): IndexStatus | undefined {
  return data.status ?? data.index;
}

/** The status displayed in the header. */
export function describeStatus(data: DashboardData): { kind: StatusKind; text: string } {
  const host = data.host;
  if (!host) {
    return { kind: 'unknown', text: 'Connecting\u2026' };
  }
  if (!host.serverRunning) {
    return { kind: 'stopped', text: `Server not running${host.stoppedReason ? ` (${host.stoppedReason})` : ''}` };
  }
  const index = currentStatus(data);
  if (!index) {
    return { kind: 'unknown', text: 'Loading\u2026' };
  }
  switch (index.status) {
    case 'idle':
      return { kind: 'ok', text: 'Idle' };
    case 'busy': {
      const progress = data.progress;
      const percent = progress && progress.total > 0
        ? ` ${Math.min(100, Math.floor((progress.completed * 100) / progress.total))}%`
        : '';
      return { kind: 'busy', text: `Indexing${percent}` };
    }
    case 'verifying':
      return { kind: 'busy', text: 'Verifying the index of the last session' };
    case 'paused':
      return { kind: 'paused', text: 'Paused' };
    case 'yield':
      return { kind: 'paused', text: 'Paused by the server (too many file system changes)' };
  }
}

/**
 * The actions enabled for the state of the server: pause while indexing is active, resume while it
 * is paused, rebuild when idle (or verifying the cached index), clear the cache when the server
 * has one; restart and logs always.
 */
export function enabledActions(
  host: HostState | undefined,
  status: IndexingStatus | undefined,
  cacheEnabled = false,
): Record<ActionId, boolean> {
  const running = host?.serverRunning === true;
  const known = running && status !== undefined;
  return {
    pause: known && (status === 'idle' || status === 'busy' || status === 'verifying'),
    resume: known && (status === 'paused' || status === 'yield'),
    rebuild: known && (status === 'idle' || status === 'verifying'),
    restartServer: host !== undefined,
    showOutput: host !== undefined,
    openLogDirectory: host?.logDirectory !== undefined,
    toggleTrigramFilters: running,
    clearCache: known && cacheEnabled,
    openCacheSettings: host?.cacheSettings !== undefined,
  };
}

/** Splits "Label: value" (lines of the status helpers) into a key/value row. */
function toKeyValue(line: string): KeyValue {
  const i = line.indexOf(': ');
  const label = i >= 0 ? line.substring(0, i) : line;
  const value = i >= 0 ? line.substring(i + 2) : '';
  return { key: label, label, value };
}

/** "9.4 GB on disk (120 MB dead) · saved 2:42 PM · changed" (or "disabled", "not saved yet"). */
function describeProjectCacheState(project: ProjectDetails, options: FormatOptions): string {
  const cache = project.cache;
  if (!cache.enabled) {
    return 'disabled';
  }
  const parts: string[] = [];
  if (cache.lastSavedUtc) {
    const dead = cache.deadBytes > 0 ? ` (${formatBytes(cache.deadBytes)} dead)` : '';
    parts.push(`${formatBytes(cache.sizeOnDiskBytes)} on disk${dead}`, `saved ${options.formatTime(cache.lastSavedUtc)}`);
  } else {
    parts.push('not saved yet');
  }
  if (cache.saving) {
    parts.push('saving\u2026');
  } else if (cache.dirty) {
    parts.push('changed since');
  }
  return parts.join(' \u00b7 ');
}

function durationRow(key: string, name: string, stats: { count: number; minMs: number; avgMs: number; maxMs: number; lastMs: number }, note: string): ProjectOperationRow {
  const none = stats.count === 0;
  const format = (ms: number) => (none ? '\u2014' : formatDuration(ms));
  return {
    key,
    name,
    count: formatCount(stats.count),
    min: format(stats.minMs),
    avg: format(stats.avgMs),
    max: format(stats.maxMs),
    last: format(stats.lastMs),
    note: none ? '' : note,
  };
}

/** The operations of a project: initial loads and incremental updates (no search times). */
export function describeProjectOperations(project: ProjectDetails): ProjectOperationRow[] {
  const load = project.operations.initialLoad;
  const notes = [`scan ${formatDuration(load.scanMs)}`];
  if (load.fromCache) {
    notes.push(load.cachedIndexMs !== undefined ? `from the cache: ${formatDuration(load.cachedIndexMs)}` : 'from the cache');
  }
  return [
    durationRow('initialLoad', 'Initial load', load, notes.join(', ')),
    durationRow('incrementalUpdates', 'Incremental updates', project.operations.incrementalUpdates, ''),
  ];
}

function describeProjects(data: DashboardData, options: FormatOptions): ProjectBlock[] {
  const projects = data.details?.projects ?? [];
  const updated = currentStatus(data)?.indexLastUpdatedUtc;
  return projects.map(project => {
    const d = project.directoryDetails;
    const m = project.memory;
    const cacheState = describeProjectCacheState(project, options);
    const index: KeyValue[] = [
      { key: 'files', label: 'Files', value: `${formatCount(d.fileCount)} files \u00b7 ${formatCount(d.searchableFilesCount)} searchable \u00b7 ${formatBytes(d.searchableFilesByteLength)} of text` },
    ];
    if (isValidTime(updated)) {
      index.push({ key: 'updated', label: 'Updated', value: options.formatTime(updated), title: 'Last update of the index (all the projects)' });
    }
    const memory: KeyValue[] = [
      {
        key: 'native',
        label: 'Native',
        value: `${formatBytes(m.nativeBytes)} (${formatCount(m.arenaCount)} arenas, ${formatBytes(m.deadBytes)} dead, ${formatBytes(m.freeBytes)} free; ${formatBytes(m.largeFileBytes)} in files > 512 KB)`,
      },
      { key: 'mapped', label: 'Mapped', value: `${formatBytes(m.mappedCacheBytes)} from the cache` },
      { key: 'trigrams', label: 'Trigram filters', value: formatBytes(m.trigramFiltersBytes) },
    ];
    const cacheSummary = project.cache.enabled
      ? (project.cache.lastSavedUtc ? `cache ${formatBytes(project.cache.sizeOnDiskBytes)}${project.cache.dirty ? ', changed' : ''}` : 'cache not saved yet')
      : 'cache disabled';
    return {
      key: project.rootPath,
      rootPath: project.rootPath,
      summary: `${formatCount(d.fileCount)} files \u00b7 native ${formatBytes(m.nativeBytes)} \u00b7 ${cacheSummary}`,
      defaultExpanded: projects.length <= ExpandedProjectsMax,
      index,
      memory,
      cache: {
        enabled: project.cache.enabled,
        writable: project.cache.writable,
        directory: project.cache.directory,
        state: cacheState,
      },
      trigramFiltersEnabled: project.trigramFiltersEnabled,
      trigramFilters: project.trigramFiltersEnabled
        ? (m.trigramFiltersBytes > 0 ? formatBytes(m.trigramFiltersBytes) : 'on')
        : 'off',
      operations: describeProjectOperations(project),
    };
  });
}

/** The memory of the whole process (the details per project are in the projects). */
function describeMemorySection(data: DashboardData): KeyValue[] {
  const rows: KeyValue[] = [];
  const status = currentStatus(data);
  const server = data.server;
  const gcHeap = server?.gcHeapBytes ?? status?.serverGcMemoryUsage;
  if (gcHeap !== undefined) {
    rows.push({
      key: 'managed',
      label: 'Managed',
      value: formatBytes(gcHeap) + (server ? ` (${formatBytes(server.gcTotalAllocatedBytes)} allocated since the start)` : ''),
    });
  }
  const native = server?.nativeAllocatedBytes ?? status?.serverNativeMemoryUsage;
  if (native !== undefined) {
    rows.push({ key: 'native', label: 'Native', value: `${formatBytes(native)} (file contents of all the projects)` });
  }
  const mapped = data.index?.fileContentsArenas.cachedFileBytes;
  if (mapped !== undefined) {
    rows.push({ key: 'mapped', label: 'Mapped', value: `${formatBytes(mapped)} from the index caches` });
  }
  if (server) {
    rows.push({
      key: 'process',
      label: 'Process',
      value: `working set ${formatBytes(server.workingSetBytes)} (peak ${formatBytes(server.peakWorkingSetBytes)}), private ${formatBytes(server.privateBytes)}`,
    });
  }
  return rows;
}

/** The index cache of the whole process: root directory and total size of the caches. */
function describeIndexCacheSection(data: DashboardData, options: FormatOptions): KeyValue[] {
  const rows: KeyValue[] = [];
  const statistics = data.server?.indexCache;
  const directory = statistics?.cacheDirectory;
  if (directory) {
    rows.push({ key: 'directory', label: 'Directory', value: directory, title: 'Root directory of the caches (a sub-directory per project)' });
  }
  const state = currentStatus(data)?.indexCache;
  if (state) {
    const projects = statistics && statistics.enabled
      ? ` \u00b7 ${formatCount(statistics.projectCount)} ${statistics.projectCount === 1 ? 'project' : 'projects'}` +
        (statistics.readOnlyProjectCount > 0 ? ` (${formatCount(statistics.readOnlyProjectCount)} read-only)` : '')
      : '';
    rows.push({ key: 'total', label: 'Total', value: toKeyValue(describeIndexCache(state, options.formatTime)).value + projects });
  }
  return rows;
}

/** Search times are not displayed (decision of the dashboard v2 design). */
function isSearchTiming(name: string): boolean {
  return /^Request:search\//.test(name) || /^Search/.test(name);
}

function describeOperations(timings: OperationTiming[] | undefined): OperationRow[] {
  return [...(timings ?? [])]
    .filter(t => !isSearchTiming(t.name))
    .sort((a, b) => a.name.localeCompare(b.name))
    .map(t => ({
      key: t.name,
      name: t.name,
      count: formatCount(t.count),
      last: formatDuration(t.lastMs),
      max: formatDuration(t.maxMs),
      average: t.count > 0 ? formatDuration(t.totalMs / t.count) : '',
      total: formatDuration(t.totalMs),
      recentMs: t.recentMs ?? [],
      recentTitle: (t.recentMs ?? []).length > 1
        ? `Last ${t.recentMs.length} durations (oldest first): ${t.recentMs.map(formatDuration).join(', ')}`
        : (t.recentMs ?? []).length === 1 ? `Last duration: ${formatDuration(t.recentMs[0])}` : '',
    }));
}

/**
 * CPU usage of the server between two statistics, in percent of the machine (all processors), or
 * undefined if it can't be computed (first statistics, server restarted).
 */
export function cpuUsagePercent(previous: ServerStatistics | undefined, current: ServerStatistics): number | undefined {
  if (!previous || previous.processId !== current.processId) {
    return undefined;
  }
  const elapsed = current.uptimeMs - previous.uptimeMs;
  if (elapsed <= 0 || current.processorCount <= 0) {
    return undefined;
  }
  const cpu = Math.max(0, current.cpuTotalMs - previous.cpuTotalMs);
  return Math.min(100, (cpu * 100) / (elapsed * current.processorCount));
}

function describeServerSection(data: DashboardData): KeyValue[] {
  const server = data.server;
  if (!server) {
    return [];
  }
  const rows: KeyValue[] = [];
  if (data.host?.serverVersion) {
    rows.push({ key: 'version', label: 'Version', value: data.host.serverVersion });
  }
  const usage = cpuUsagePercent(data.previousServer, server);
  rows.push(
    { key: 'runtime', label: 'Runtime', value: server.runtimeDescription },
    { key: 'process', label: 'Process', value: `pid ${server.processId}, ${server.processorCount} processors` },
    { key: 'uptime', label: 'Uptime', value: formatDuration(server.uptimeMs) },
    {
      key: 'cpu',
      label: 'CPU',
      value: `${formatDuration(server.cpuTotalMs)} (user ${formatDuration(server.cpuUserMs)}, kernel ${formatDuration(server.cpuKernelMs)})` +
        (usage !== undefined ? `, ${usage.toFixed(1)}% now` : ''),
      title: 'CPU time since the start; "now": percentage of all the processors since the previous statistics',
    },
    { key: 'threads', label: 'Threads', value: formatCount(server.threadCount) },
    { key: 'handles', label: 'Handles', value: formatCount(server.handleCount) },
    {
      key: 'gc',
      label: 'Garbage collections',
      value: `gen0 ${formatCount(server.gcGen0Collections)}, gen1 ${formatCount(server.gcGen1Collections)}, ` +
        `gen2 ${formatCount(server.gcGen2Collections)}, total pause ${formatDuration(server.gcTotalPauseMs)}`,
    },
  );
  return rows;
}

function describeFacts(data: DashboardData, options: FormatOptions): string[] {
  const facts: string[] = [];
  const index = currentStatus(data);
  if (index) {
    facts.push(`${formatCount(index.projectCount)} ${index.projectCount === 1 ? 'project' : 'projects'}`);
    facts.push(`${formatCount(index.fileCount)} files`);
    if (isValidTime(index.indexLastUpdatedUtc)) {
      facts.push(`index updated ${options.formatTime(index.indexLastUpdatedUtc)}`);
    }
  }
  if (data.host?.serverVersion) {
    facts.push(`server ${data.host.serverVersion}`);
  }
  const server = data.server;
  if (server) {
    facts.push(server.runtimeDescription, `pid ${server.processId}`, `uptime ${formatDuration(server.uptimeMs)}`);
  }
  return facts;
}

/** The scale of a bytes axis: a nice maximum in MB or GB, and its labeled ticks. */
export function bytesScale(maxBytes: number): { max: number; ticks: { value: number; label: string }[] } {
  const [unit, name] = maxBytes >= GB ? [GB, 'GB'] : [MB, 'MB'];
  const scale = niceScale(maxBytes / unit);
  return {
    max: scale.max * unit,
    ticks: scale.ticks.map(t => ({ value: t * unit, label: t === 0 ? '0' : `${formatCount(t)} ${name}` })),
  };
}

/** The scale of a percent axis: at least 10%, at most 100%. */
export function percentScale(maxPercent: number): { max: number; ticks: { value: number; label: string }[] } {
  const scale = niceScale(Math.min(100, Math.max(10, maxPercent)));
  return { max: scale.max, ticks: scale.ticks.map(t => ({ value: t, label: `${t}%` })) };
}

function historyChart(
  key: string,
  history: ServerHistory,
  unit: 'bytes' | 'percent',
  series: ChartSeries[],
  version: string,
): HistoryChart {
  const endMs = lastSampleTime(history)!;
  const startMs = endMs - HistoryWindowMs;
  const max = seriesMax(history.timesUnixMs, series.map(s => s.values), startMs);
  const scale = unit === 'bytes' ? bytesScale(max) : percentScale(max);
  return {
    key,
    version: `${version}:${history.timesUnixMs.length}:${endMs}`,
    unit,
    times: history.timesUnixMs,
    intervalMs: history.intervalMs,
    startMs,
    endMs,
    yMax: scale.max,
    yTicks: scale.ticks,
    xTicks: timeTicks(endMs),
    series,
  };
}

/** The charts of the history of the server (none until the first samples are received). */
function describeCharts(data: DashboardData): Pick<DashboardViewModel, 'memoryChart' | 'cpuChart' | 'cacheChart' | 'statusTimeline'> {
  const history = data.history;
  if (!data.host?.serverRunning || !history || history.timesUnixMs.length === 0) {
    return {};
  }
  const memoryChart = historyChart('memory', history, 'bytes', [
    { key: 'gc', label: 'GC heap', color: 0, values: history.gcHeapBytes },
    { key: 'native', label: 'Native', color: 1, values: history.nativeAllocatedBytes },
    { key: 'workingSet', label: 'Working set', color: 2, values: history.workingSetBytes },
    { key: 'private', label: 'Private bytes', color: 3, values: history.privateBytes },
  ], 'memory');
  const processorCount = data.server?.processorCount;
  const cpuChart = processorCount
    ? historyChart('cpu', history, 'percent', [
      { key: 'cpu', label: 'CPU', color: 0, values: cpuPercentSeries(history.timesUnixMs, history.cpuTotalMs, processorCount) },
    ], `cpu${processorCount}`)
    : undefined;
  const cacheEnabled = currentStatus(data)?.indexCache?.enabled === true;
  const cacheChart = cacheEnabled
    ? historyChart('cache', history, 'bytes', [
      { key: 'cache', label: 'Size on disk', color: 0, values: history.cacheSizeOnDiskBytes },
    ], 'cache')
    : undefined;
  const endMs = memoryChart.endMs;
  const statusTimeline: StatusTimeline = {
    version: memoryChart.version,
    startMs: memoryChart.startMs,
    endMs,
    segments: statusSegments(history.timesUnixMs, history.status, history.intervalMs, memoryChart.startMs),
  };
  return { memoryChart, cpuChart, cacheChart, statusTimeline };
}

function normalizePath(p: string): string {
  const trimmed = p.trim().replace(/[\\/]+$/, '');
  return /^[a-zA-Z]:|\\/.test(trimmed) ? trimmed.toLowerCase() : trimmed;
}

/**
 * The cache settings of the host, and whether the cache directory differs from the one of the
 * running server (applied when the server restarts).
 */
export function describeCacheSettings(data: DashboardData): CacheSettingsView | undefined {
  const settings = data.host?.cacheSettings;
  if (!settings) {
    return undefined;
  }
  const directory = settings.directory.trim();
  let restartNeeded = false;
  // Note: The cache directory is applied when the server starts (the cache on/off setting is
  // applied per project at any time).
  const serverDirectory = data.server?.indexCache?.cacheDirectory;
  if (data.host?.serverRunning && directory && serverDirectory) {
    restartNeeded = normalizePath(directory) !== normalizePath(serverDirectory);
  }
  return { enabled: settings.enabled, directory: directory || 'default', restartNeeded };
}

/** The view model of the page for `data`. */
export function buildViewModel(data: DashboardData, options: FormatOptions = defaultFormatOptions): DashboardViewModel {
  const current = data.host?.serverRunning ? currentStatus(data) : undefined;
  const status = current?.status;
  return {
    status: describeStatus(data),
    facts: data.host?.serverRunning ? describeFacts(data, options) : [],
    actions: enabledActions(data.host, status,
      current?.indexCache?.enabled === true || (data.details?.projects ?? []).some(p => p.cache.enabled)),
    pauseOrResume: status === 'paused' || status === 'yield' ? 'resume' : 'pause',
    error: data.error,
    trigramFiltersSetting: data.host?.trigramFiltersSetting === true,
    projects: describeProjects(data, options),
    memory: describeMemorySection(data),
    indexCache: describeIndexCacheSection(data, options),
    cacheSettings: describeCacheSettings(data),
    operations: describeOperations(data.server?.operationTimings),
    server: describeServerSection(data),
    ...describeCharts(data),
  };
}
