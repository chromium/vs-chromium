// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Messages exchanged between the server dashboard page (media: out/media/dashboard.js) and its
// host (the VS Code extension today, a Visual Studio tool window with WebView2 later).
//
// The page is host neutral: it sends protocol requests (method names and parameters of the
// generated protocol, forwarded as is to the server by the host), receives their results and the
// "index/..." notifications of the server, and sends a few UI commands for what only the host can
// do (restart the server, show its logs, change a setting). Keep this file free of runtime
// dependencies: it is compiled into the page.

/** The protocol requests the page may send (the host rejects any other method). */
export const DashboardRequestMethods = [
  'index/statistics',
  'index/status',
  'index/clearCache',
  'index/details',
  'index/pause',
  'index/resume',
  'project/details',
  'project/setOptions',
  'project/rebuild',
  'project/clearCache',
  'server/statistics',
  'server/history',
  'tree/refresh',
] as const;

export type DashboardRequestMethod = typeof DashboardRequestMethods[number];

export function isDashboardRequestMethod(method: unknown): method is DashboardRequestMethod {
  return typeof method === 'string' && (DashboardRequestMethods as readonly string[]).includes(method);
}

/** Commands only the host can execute. */
export type HostCommand =
  | { command: 'restartServer' }
  | { command: 'showOutput' }
  | { command: 'openLogDirectory' }
  /** Enables or disables the trigram filters of all the projects (the host setting). */
  | { command: 'setTrigramFiltersSetting'; enabled: boolean }
  /**
   * Enables or disables the trigram filters of a project, until the setting changes or the server
   * restarts (the host decides how this combines with its setting).
   */
  | { command: 'setProjectTrigramFilters'; projectPath: string; enabled: boolean }
  /**
   * Deletes the index cache of a project, or of all the projects ("index/clearCache"), after a
   * confirmation of the user by the host.
   */
  | { command: 'clearCache'; projectPath?: string }
  /** Shows the cache settings of the host (e.g. the VS Code settings, a Visual Studio options page). */
  | { command: 'openCacheSettings' };

/** Messages from the page to the host. */
export type PageToHost =
  /** The page is loaded and listening: the host answers with "hostState" and "visibility". */
  | { type: 'ready' }
  | { type: 'request'; id: number; method: DashboardRequestMethod; params?: unknown }
  | ({ type: 'command' } & HostCommand);

/** What the page knows about its host and the server process. */
export interface HostState {
  /** Name of the host, for display (e.g. "VS Code"). */
  hostName: string;
  serverRunning: boolean;
  /** Why the server is not running (e.g. its exit reason). */
  stoppedReason?: string;
  serverVersion?: string;
  /** The host setting enabling the trigram filters of all the projects. */
  trigramFiltersSetting: boolean;
  /** Directory of the log files of the server, if known. */
  logDirectory?: string;
  /** The cache settings of the host, applied when the server (re)starts (absent if the host has none). */
  cacheSettings?: { enabled: boolean; directory: string };
}

/** Messages from the host to the page. */
export type HostToPage =
  | { type: 'hostState'; state: HostState }
  /** The page is visible (it refreshes its data periodically only while visible). */
  | { type: 'visibility'; visible: boolean }
  | { type: 'response'; id: number; result?: unknown; error?: string }
  /** A notification of the server ("index/stateChanged", "index/progress", ...). */
  | { type: 'notification'; method: string; params: unknown };
