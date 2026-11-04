// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import type {
  DetailsParams,
  IndexDetails,
  IndexStateChangedParams,
  IndexStatistics,
  IndexStatus,
  ServerHistory,
  ProgressParams,
  ServerStatistics,
} from '../../protocol/protocol.g';
import type { DashboardRequestMethod, HostCommand, HostToPage } from './dashboardMessages';
import { lastSampleTime, mergeHistory } from './dashboardCharts';
import { buildViewModel, DashboardData, DashboardViewModel, FormatOptions, planRefresh, RefreshIntervalMs, RefreshPlan } from './dashboardModel';
import type { HostShim } from './hostShim';

// Logic of the dashboard page, free of DOM dependencies (unit tested with a fake host): requests
// the data periodically while the page is visible (and soon after the notifications of the server),
// tracks the responses of the host and executes the actions of the user.

/** Delay of the refresh following a notification of the server (notifications come in bursts). */
export const NotificationRefreshDelayMs = 250;
/** Requests without response after this delay fail (the refreshes don't stop). */
export const RequestTimeoutMs = 30_000;

export interface Timers {
  setTimeout(callback: () => void, ms: number): unknown;
  clearTimeout(handle: unknown): void;
}

const defaultTimers: Timers = {
  setTimeout: (callback, ms) => setTimeout(callback, ms),
  clearTimeout: handle => clearTimeout(handle as ReturnType<typeof setTimeout>),
};

/** The user actions of the page. */
export type DashboardAction =
  | { action: 'pause' }
  | { action: 'resume' }
  | { action: 'rebuild' }
  | { action: 'restartServer' }
  | { action: 'showOutput' }
  | { action: 'openLogDirectory' }
  | { action: 'toggleTrigramFiltersSetting' }
  | { action: 'setProjectTrigramFilters'; projectPath: string; enabled: boolean }
  | { action: 'clearCache'; projectPath?: string }
  | { action: 'openCacheSettings' }
  | { action: 'rebuildProject'; projectPath: string }
  | { action: 'setProjectCacheEnabled'; projectPath: string; enabled: boolean }
  | { action: 'openProjectCacheFolder'; projectPath: string };

const detailsParams: DetailsParams = { maxFilesByExtensionDetailsCount: 0, maxLargeFilesDetailsCount: 0 };

interface PendingRequest {
  resolve(result: unknown): void;
  reject(error: Error): void;
  timer: unknown;
}

export class DashboardController {
  private readonly data: DashboardData = {};
  private readonly pending = new Map<number, PendingRequest>();
  private nextRequestId = 1;
  private hostVisible = true;
  private pageVisible = true;
  /** Number of refreshes since the page became visible. */
  private refreshCount = 0;
  private refreshing = false;
  /** A refresh was requested while one was running. */
  private refreshAgain = false;
  private forced: Partial<RefreshPlan> = { statistics: true, details: true };
  private timer: unknown;
  private unsubscribe: (() => void) | undefined;
  private disposed = false;

  constructor(
    private readonly host: HostShim,
    private readonly render: (viewModel: DashboardViewModel) => void,
    private readonly timers: Timers = defaultTimers,
    private readonly formatOptions?: FormatOptions,
  ) {}

  /** Starts listening to the host and tells it the page is ready. */
  start(): void {
    this.unsubscribe = this.host.onMessage(message => this.handleMessage(message));
    this.host.post({ type: 'ready' });
    this.update();
  }

  dispose(): void {
    this.disposed = true;
    this.stopTimer();
    this.unsubscribe?.();
    for (const [, request] of this.pending) {
      this.timers.clearTimeout(request.timer);
      request.reject(new Error('Disposed'));
    }
    this.pending.clear();
  }

  /** The visibility of the document (e.g. "visibilitychange" events of the page). */
  setPageVisible(visible: boolean): void {
    this.pageVisible = visible;
    this.onVisibilityChanged();
  }

  get visible(): boolean {
    return this.hostVisible && this.pageVisible && !this.disposed;
  }

  /** The current view model (for tests). */
  get viewModel(): DashboardViewModel {
    return buildViewModel(this.data, this.formatOptions);
  }

  handleMessage(message: HostToPage): void {
    switch (message.type) {
      case 'hostState': {
        const wasRunning = this.data.host?.serverRunning === true;
        this.data.host = message.state;
        if (!message.state.serverRunning) {
          // Note: The statistics of a server that stopped are not displayed.
          this.data.status = undefined;
          this.data.index = undefined;
          this.data.history = undefined;
          this.data.server = undefined;
          this.data.previousServer = undefined;
          this.data.details = undefined;
          this.data.progress = undefined;
          this.data.error = undefined;
        } else if (!wasRunning) {
          this.refreshSoon({ statistics: true, details: true }, 0);
        }
        this.update();
        break;
      }
      case 'visibility':
        this.hostVisible = message.visible;
        this.onVisibilityChanged();
        break;
      case 'response': {
        const request = this.pending.get(message.id);
        if (request) {
          this.pending.delete(message.id);
          this.timers.clearTimeout(request.timer);
          if (message.error !== undefined) {
            request.reject(new Error(message.error));
          } else {
            request.resolve(message.result);
          }
        }
        break;
      }
      case 'notification':
        this.handleNotification(message.method, message.params);
        break;
    }
  }

  /** Executes an action of the user. */
  async execute(action: DashboardAction): Promise<void> {
    try {
      switch (action.action) {
        case 'pause':
          await this.request('index/pause');
          break;
        case 'resume':
          await this.request('index/resume');
          break;
        case 'rebuild':
          await this.request('tree/refresh');
          break;
        case 'restartServer':
          this.command({ command: 'restartServer' });
          return;
        case 'showOutput':
          this.command({ command: 'showOutput' });
          return;
        case 'openLogDirectory':
          this.command({ command: 'openLogDirectory' });
          return;
        case 'toggleTrigramFiltersSetting':
          this.command({ command: 'setTrigramFiltersSetting', enabled: !this.data.host?.trigramFiltersSetting });
          break;
        case 'setProjectTrigramFilters':
          this.command({ command: 'setProjectTrigramFilters', projectPath: action.projectPath, enabled: action.enabled });
          break;
        case 'clearCache':
          // Note: The host asks for a confirmation, then sends "index/clearCache".
          this.command(action.projectPath !== undefined ? { command: 'clearCache', projectPath: action.projectPath } : { command: 'clearCache' });
          break;
        case 'openCacheSettings':
          this.command({ command: 'openCacheSettings' });
          return;
        case 'rebuildProject':
          // Note: The host asks for a confirmation, then sends "project/rebuild".
          this.command({ command: 'rebuildProject', projectPath: action.projectPath });
          break;
        case 'setProjectCacheEnabled':
          this.command({ command: 'setProjectCacheEnabled', projectPath: action.projectPath, enabled: action.enabled });
          break;
        case 'openProjectCacheFolder':
          this.command({ command: 'openProjectCacheFolder', projectPath: action.projectPath });
          return;
      }
    } catch (e) {
      this.data.error = errorMessage(e);
      this.update();
      return;
    }
    // Note: The project options are applied by the host (asynchronously): the details are
    // requested a bit later.
    this.refreshSoon({ statistics: true, details: true }, NotificationRefreshDelayMs);
  }

  /** Sends a protocol request to the host (forwarded to the server). */
  request<R>(method: DashboardRequestMethod, params?: unknown): Promise<R> {
    const id = this.nextRequestId++;
    return new Promise<R>((resolve, reject) => {
      const timer = this.timers.setTimeout(() => {
        if (this.pending.delete(id)) {
          reject(new Error(`No response to "${method}"`));
        }
      }, RequestTimeoutMs);
      this.pending.set(id, { resolve: resolve as (result: unknown) => void, reject, timer });
      this.host.post({ type: 'request', id, method, params });
    });
  }

  private command(command: HostCommand) {
    this.host.post({ type: 'command', ...command });
  }

  private handleNotification(method: string, params: unknown) {
    switch (method) {
      case 'index/progress':
        this.data.progress = params as ProgressParams;
        this.update();
        return; // Note: Up to 10 per second: no refresh.
      case 'index/stateChanged': {
        const state = params as IndexStateChangedParams;
        const update = { status: state.status, indexLastUpdatedUtc: state.lastIndexUpdatedUtc };
        if (this.data.status) {
          this.data.status = { ...this.data.status, ...update };
        }
        if (this.data.index) {
          this.data.index = { ...this.data.index, ...update };
        }
        if (state.status !== 'busy') {
          this.data.progress = undefined;
        }
        this.update();
        this.refreshSoon({ details: state.status === 'idle' }, NotificationRefreshDelayMs);
        return;
      }
      case 'index/loaded':
      case 'index/scanFinished':
        this.data.progress = undefined;
        this.refreshSoon({ statistics: true, details: true }, NotificationRefreshDelayMs);
        return;
      default:
        this.refreshSoon({}, NotificationRefreshDelayMs);
        return;
    }
  }

  private onVisibilityChanged() {
    if (this.visible) {
      // Note: Everything is requested again when the page becomes visible.
      this.refreshCount = 0;
      this.refreshSoon({}, 0);
    } else {
      this.stopTimer();
    }
  }

  /** Schedules a refresh in `delayMs` (if visible), unless one is scheduled sooner. */
  private refreshSoon(forced: Partial<RefreshPlan>, delayMs: number) {
    this.forced = {
      statistics: this.forced.statistics || forced.statistics,
      details: this.forced.details || forced.details,
    };
    if (!this.visible) {
      return;
    }
    if (this.refreshing) {
      this.refreshAgain = true;
      return;
    }
    this.stopTimer();
    this.timer = this.timers.setTimeout(() => {
      this.timer = undefined;
      void this.refresh();
    }, delayMs);
  }

  private stopTimer() {
    if (this.timer !== undefined) {
      this.timers.clearTimeout(this.timer);
      this.timer = undefined;
    }
  }

  /** Requests the data of the page (see planRefresh), then schedules the next refresh. */
  async refresh(): Promise<void> {
    if (this.refreshing || this.disposed) {
      return;
    }
    if (!this.data.host?.serverRunning) {
      this.scheduleNextRefresh();
      return;
    }
    this.refreshing = true;
    const plan = planRefresh(this.refreshCount++, this.forced);
    this.forced = {};
    try {
      const since = lastSampleTime(this.data.history);
      const [status, index, server, history, details] = await Promise.all([
        this.request<IndexStatus>('index/status'),
        plan.statistics ? this.request<IndexStatistics>('index/statistics', {}) : Promise.resolve(undefined),
        plan.statistics ? this.request<ServerStatistics>('server/statistics', {}) : Promise.resolve(undefined),
        plan.statistics
          ? this.request<ServerHistory>('server/history', since !== undefined ? { sinceUnixMs: since } : {})
          : Promise.resolve(undefined),
        plan.details ? this.request<IndexDetails>('index/details', detailsParams) : Promise.resolve(undefined),
      ]);
      if (this.data.host?.serverRunning) {
        this.data.status = status;
        // Note: The arena statistics are refreshed less often than the status.
        this.data.index = index ? { ...index, ...status } : this.data.index && { ...this.data.index, ...status };
        if (server) {
          if (this.data.server && this.data.server.processId !== server.processId) {
            // Another server process: its history starts again.
            this.data.history = undefined;
            this.data.server = undefined;
          }
          this.data.previousServer = this.data.server;
          this.data.server = server;
        }
        if (history) {
          this.data.history = mergeHistory(this.data.history, history);
        }
        if (details) {
          this.data.details = details;
        }
        if (status.status !== 'busy') {
          this.data.progress = undefined;
        }
        this.data.error = undefined;
      }
    } catch (e) {
      if (this.disposed) {
        return;
      }
      this.data.error = errorMessage(e);
    } finally {
      this.refreshing = false;
    }
    this.update();
    if (this.refreshAgain) {
      this.refreshAgain = false;
      this.refreshSoon({}, 0);
    } else {
      this.scheduleNextRefresh();
    }
  }

  private scheduleNextRefresh() {
    if (this.visible && this.timer === undefined) {
      this.timer = this.timers.setTimeout(() => {
        this.timer = undefined;
        void this.refresh();
      }, RefreshIntervalMs);
    }
  }

  private update() {
    if (!this.disposed) {
      this.render(this.viewModel);
    }
  }
}

function errorMessage(e: unknown): string {
  return e instanceof Error ? e.message : String(e);
}
