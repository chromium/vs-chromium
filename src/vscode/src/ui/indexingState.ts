// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import { NotificationType } from 'vscode-jsonrpc';
import {
  FilesLoadedParams,
  IndexLoadedNotification,
  IndexLoadingNotification,
  IndexLoadingProgressNotification,
  IndexProgressNotification,
  IndexScanFinishedNotification,
  IndexScanStartedNotification,
  IndexStateChangedNotification,
  IndexStateChangedParams,
  OperationStartedParams,
  ProgressParams,
  ScanFinishedParams,
} from '../protocol/protocol.g';

/** Indexing state displayed to the user, derived from server notifications. */
export type IndexingState =
  | { kind: 'starting' }
  | { kind: 'scanning' }
  | { kind: 'loading'; progress?: { text?: string; completed: number; total: number } }
  | { kind: 'ready'; fileCount?: number; projectCount?: number }
  | { kind: 'paused'; yielded: boolean }
  | { kind: 'noProject' }
  | { kind: 'error'; message: string }
  | { kind: 'stopped'; message: string };

/** A notification of the server ("index/..."), relevant to the indexing state. */
export type IndexEvent =
  | { type: 'scanStarted'; params: OperationStartedParams }
  | { type: 'scanFinished'; params: ScanFinishedParams }
  | { type: 'loading'; params: OperationStartedParams }
  | { type: 'loadingProgress'; params: OperationStartedParams }
  | { type: 'loaded'; params: FilesLoadedParams }
  | { type: 'progress'; params: ProgressParams }
  | { type: 'stateChanged'; params: IndexStateChangedParams };

/** Source of server notifications (`ServerClient`). */
export interface NotificationSource {
  onNotification<P>(type: NotificationType<P>, listener: (params: P) => void): () => void;
}

/** Calls `listener` for each "index/..." notification of `source`. Returns a function to unsubscribe. */
export function subscribeIndexEvents(source: NotificationSource, listener: (event: IndexEvent) => void): () => void {
  const unsubscribes = [
    source.onNotification(IndexScanStartedNotification.type, params => listener({ type: 'scanStarted', params })),
    source.onNotification(IndexScanFinishedNotification.type, params => listener({ type: 'scanFinished', params })),
    source.onNotification(IndexLoadingNotification.type, params => listener({ type: 'loading', params })),
    source.onNotification(IndexLoadingProgressNotification.type, params => listener({ type: 'loadingProgress', params })),
    source.onNotification(IndexLoadedNotification.type, params => listener({ type: 'loaded', params })),
    source.onNotification(IndexProgressNotification.type, params => listener({ type: 'progress', params })),
    source.onNotification(IndexStateChangedNotification.type, params => listener({ type: 'stateChanged', params })),
  ];
  return () => unsubscribes.forEach(unsubscribe => unsubscribe());
}

/**
 * Computes the next indexing state from a server notification. Returns `undefined` if the
 * notification does not change the state. Kept free of VS Code dependencies so it can be unit
 * tested.
 */
export function nextIndexingState(state: IndexingState, event: IndexEvent): IndexingState | undefined {
  switch (event.type) {
    case 'scanStarted':
      return { kind: 'scanning' };
    case 'scanFinished':
      if (event.params.error) {
        return { kind: 'error', message: event.params.error.message || 'Error indexing files' };
      }
      return { kind: 'loading' };
    case 'loading':
      return { kind: 'loading' };
    case 'loadingProgress':
      return state.kind === 'loading' ? undefined : { kind: 'loading' };
    case 'progress':
      if (state.kind === 'scanning' || state.kind === 'loading') {
        return {
          kind: 'loading',
          progress: { text: event.params.displayText || undefined, completed: event.params.completed, total: event.params.total },
        };
      }
      return undefined;
    case 'loaded':
      if (event.params.error) {
        return { kind: 'error', message: event.params.error.message || 'Error loading files' };
      }
      return { kind: 'ready' };
    case 'stateChanged':
      switch (event.params.status) {
        case 'paused':
        case 'yield':
          return { kind: 'paused', yielded: event.params.status === 'yield' };
        case 'idle':
          return state.kind === 'paused' ? { kind: 'ready' } : undefined;
        default:
          // "busy": the scan/load notifications report the progress.
          return undefined;
      }
  }
}

/** Text and tooltip for the status bar item. */
export function describeIndexingState(state: IndexingState): { text: string; tooltip: string } {
  switch (state.kind) {
    case 'starting':
      return { text: '$(sync~spin) VsChromium', tooltip: 'VsChromium: starting indexing server...' };
    case 'scanning':
      return { text: '$(sync~spin) VsChromium: scanning', tooltip: 'VsChromium: scanning file system...' };
    case 'loading': {
      const p = state.progress;
      const percent = p && p.total > 0 ? ` ${Math.floor((p.completed * 100) / p.total)}%` : '';
      return {
        text: `$(sync~spin) VsChromium: indexing${percent}`,
        tooltip: `VsChromium: ${p?.text ?? 'loading files into the search index...'}`,
      };
    }
    case 'ready': {
      const files = state.fileCount !== undefined ? ` (${state.fileCount.toLocaleString()} files)` : '';
      return { text: '$(database) VsChromium', tooltip: `VsChromium: index ready${files}. Click for details.` };
    }
    case 'paused':
      return {
        text: '$(debug-pause) VsChromium',
        tooltip: state.yielded
          ? 'VsChromium: indexing paused by the server (too many file system changes). Click to resume.'
          : 'VsChromium: indexing paused. Click to resume.',
      };
    case 'noProject':
      return {
        text: '$(circle-slash) VsChromium',
        tooltip: 'VsChromium: no project found in this workspace (add a "vs-chromium-project.txt" file at the root).',
      };
    case 'error':
      return { text: '$(error) VsChromium', tooltip: `VsChromium: ${state.message}` };
    case 'stopped':
      return { text: '$(debug-disconnect) VsChromium', tooltip: `VsChromium: ${state.message}. Click to restart.` };
  }
}
