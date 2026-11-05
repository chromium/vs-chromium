// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import { RequestType, RequestType0 } from 'vscode-jsonrpc';
import {
  IndexDetailsRequest,
  IndexClearCacheRequest,
  IndexStatusRequest,
  ServerHistoryRequest,
  IndexLoadedNotification,
  IndexLoadingNotification,
  IndexLoadingProgressNotification,
  IndexPauseRequest,
  IndexProgressNotification,
  IndexResumeRequest,
  IndexScanFinishedNotification,
  IndexScanStartedNotification,
  IndexStateChangedNotification,
  IndexStatisticsRequest,
  InitializeResult,
  ProjectCancelMeasureRequest,
  ProjectClearCacheRequest,
  ProjectDetailsRequest,
  ProjectDirectorySizesRequest,
  ProjectMeasureDirectoryRequest,
  ProjectRebuildRequest,
  ProjectSetOptionsRequest,
  ServerStatisticsRequest,
  TreeRefreshRequest,
} from '../../protocol/protocol.g';
import { IndexEvent, NotificationSource, subscribeIndexEvents } from '../indexingState';
import {
  DashboardRequestMethod,
  HostCommand,
  HostState,
  HostToPage,
  isDashboardRequestMethod,
  PageToHost,
} from './dashboardMessages';

// The host side of the dashboard page, free of VS Code dependencies (unit tested): forwards the
// protocol requests of the page to the server, the "index/..." notifications of the server to the
// page, and tracks the server process (restarts, exits). The VS Code specific parts (webview
// panel, commands, settings) are in dashboardPanel.ts. A Visual Studio host would implement the
// same with its generated C# client.

/** The part of `ServerClient` used by the dashboard. */
export interface DashboardServer extends NotificationSource {
  request<P, R>(type: RequestType<P, R, void>, params: P): Promise<R>;
  request0<R>(type: RequestType0<R, void>): Promise<R>;
  onExit(listener: (reason: string) => void): () => void;
  ready(): Promise<InitializeResult>;
}

const requestTypes: Partial<Record<DashboardRequestMethod, RequestType<unknown, unknown, void>>> = {
  'index/statistics': IndexStatisticsRequest.type as RequestType<unknown, unknown, void>,
  'index/details': IndexDetailsRequest.type as RequestType<unknown, unknown, void>,
  'project/details': ProjectDetailsRequest.type as RequestType<unknown, unknown, void>,
  'project/setOptions': ProjectSetOptionsRequest.type as RequestType<unknown, unknown, void>,
  'server/statistics': ServerStatisticsRequest.type as RequestType<unknown, unknown, void>,
  'server/history': ServerHistoryRequest.type as RequestType<unknown, unknown, void>,
  'project/rebuild': ProjectRebuildRequest.type as RequestType<unknown, unknown, void>,
  'project/clearCache': ProjectClearCacheRequest.type as RequestType<unknown, unknown, void>,
  'project/directorySizes': ProjectDirectorySizesRequest.type as RequestType<unknown, unknown, void>,
  'project/measureDirectory': ProjectMeasureDirectoryRequest.type as RequestType<unknown, unknown, void>,
  'project/cancelMeasure': ProjectCancelMeasureRequest.type as RequestType<unknown, unknown, void>,
};

const requestTypes0: Partial<Record<DashboardRequestMethod, RequestType0<unknown, void>>> = {
  'index/pause': IndexPauseRequest.type,
  'index/resume': IndexResumeRequest.type,
  'tree/refresh': TreeRefreshRequest.type,
  'index/status': IndexStatusRequest.type,
  'index/clearCache': IndexClearCacheRequest.type,
};

/** The method of the notification of each index event (forwarded as is to the page). */
const notificationMethods: Record<IndexEvent['type'], string> = {
  scanStarted: IndexScanStartedNotification.method,
  scanFinished: IndexScanFinishedNotification.method,
  loading: IndexLoadingNotification.method,
  loadingProgress: IndexLoadingProgressNotification.method,
  loaded: IndexLoadedNotification.method,
  progress: IndexProgressNotification.method,
  stateChanged: IndexStateChangedNotification.method,
};

export interface DashboardBridgeOptions {
  hostName: string;
  getServer(): DashboardServer | undefined;
  post(message: HostToPage): void;
  trigramFiltersSetting(): boolean;
  logDirectory?: string;
  /** The cache settings of the host (applied when the server restarts), if it has some. */
  cacheSettings?(): { enabled: boolean; directory: string };
  /** Executes a command of the page (restart the server, show the logs, change a setting...). */
  executeCommand(command: HostCommand): Promise<void>;
  /** The message of an error of a request, for display. */
  errorMessage(e: unknown): string;
}

export class DashboardBridge {
  private server: DashboardServer | undefined;
  private serverVersion: string | undefined;
  private stoppedReason: string | undefined;
  private unsubscribe: (() => void) | undefined;
  private disposed = false;

  constructor(private readonly options: DashboardBridgeOptions) {}

  /** Handles a message of the page. */
  async handleMessage(message: PageToHost): Promise<void> {
    switch (message.type) {
      case 'ready':
        this.syncServer(/*force*/ true);
        break;
      case 'request':
        await this.forwardRequest(message.id, message.method, message.params);
        break;
      case 'command': {
        const { type: _, ...command } = message;
        await this.options.executeCommand(command as HostCommand);
        this.syncServer(/*force*/ command.command === 'setTrigramFiltersSetting');
        break;
      }
    }
  }

  /**
   * Tracks the current server (it changes when the server restarts): subscribes to its
   * notifications and sends the state of the host to the page if it changed (or if `force`).
   */
  syncServer(force = false): void {
    if (this.disposed) {
      return;
    }
    const server = this.options.getServer();
    if (server === this.server) {
      if (force) {
        this.postHostState();
      }
      return;
    }
    this.unsubscribe?.();
    this.unsubscribe = undefined;
    this.server = server;
    this.serverVersion = undefined;
    if (server) {
      this.stoppedReason = undefined;
      const unsubscribeEvents = subscribeIndexEvents(server, event => {
        if (this.server === server) {
          this.options.post({ type: 'notification', method: notificationMethods[event.type], params: event.params });
        }
      });
      const unsubscribeExit = server.onExit(reason => {
        if (this.server === server) {
          this.stoppedReason = reason;
          this.postHostState();
        }
      });
      this.unsubscribe = () => {
        unsubscribeEvents();
        unsubscribeExit();
      };
      server.ready().then(info => {
        if (this.server === server) {
          this.serverVersion = info.serverVersion;
          this.postHostState();
        }
      }, () => { /* Reported by the exit listener. */ });
    } else {
      this.stoppedReason ??= 'not started';
    }
    this.postHostState();
  }

  postHostState(): void {
    if (this.disposed) {
      return;
    }
    // Note: A server that exited is still returned until the manager drops it.
    const state: HostState = {
      hostName: this.options.hostName,
      serverRunning: this.server !== undefined && this.stoppedReason === undefined,
      stoppedReason: this.stoppedReason,
      serverVersion: this.serverVersion,
      trigramFiltersSetting: this.options.trigramFiltersSetting(),
      logDirectory: this.options.logDirectory,
      cacheSettings: this.options.cacheSettings?.(),
    };
    this.options.post({ type: 'hostState', state });
  }

  dispose(): void {
    this.disposed = true;
    this.unsubscribe?.();
    this.unsubscribe = undefined;
    this.server = undefined;
  }

  private async forwardRequest(id: number, method: unknown, params: unknown) {
    this.syncServer();
    const server = this.server;
    let response: HostToPage;
    if (!isDashboardRequestMethod(method)) {
      response = { type: 'response', id, error: `Request not allowed: ${String(method)}` };
    } else if (!server || this.stoppedReason !== undefined) {
      response = { type: 'response', id, error: 'The indexing server is not running.' };
    } else {
      try {
        const type0 = requestTypes0[method];
        const result = type0
          ? await server.request0(type0)
          : await server.request(requestTypes[method]!, params ?? {});
        response = { type: 'response', id, result: result ?? null };
      } catch (e) {
        response = { type: 'response', id, error: this.options.errorMessage(e) };
      }
    }
    if (!this.disposed) {
      this.options.post(response);
    }
  }
}
