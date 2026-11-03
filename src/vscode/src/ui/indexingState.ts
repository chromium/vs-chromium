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

/**
 * Progress of an index load, as displayed: a short phase and a whole percentage. The server sends
 * a detailed text (e.g. the path of the file being read) up to 10 times per second; displaying it
 * made the status bar tooltip flicker (its text, width and height changed constantly). The state
 * only changes when the phase or the percentage changes.
 */
export interface LoadingProgress {
  phase: LoadingPhase;
  /** Whole percentage, if the total is known. */
  percent?: number;
  /** Number of files found so far ("scanning directories" phase). */
  fileCount?: number;
}

export type LoadingPhase = 'scanning directories' | 'reading files' | 'finalizing' | 'loading files';

/** The phase of a server progress text (see the \"DisplayProgress\" calls of the server). */
export function loadingPhase(displayText: string | undefined): LoadingPhase {
  const text = displayText ?? '';
  if (text.startsWith('Traversing')) {
    return 'scanning directories';
  }
  if (text.startsWith('Reading file')) {
    return 'reading files';
  }
  if (text.startsWith('Finalizing')) {
    return 'finalizing';
  }
  return 'loading files';
}

/** Indexing state displayed to the user, derived from server notifications. */
export type IndexingState =
  | { kind: 'starting' }
  | { kind: 'scanning' }
  | { kind: 'loading'; progress?: LoadingProgress }
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
        const { completed, total } = event.params;
        const phase = loadingPhase(event.params.displayText);
        const progress: LoadingProgress = { phase };
        if (total > 0) {
          progress.percent = Math.min(100, Math.floor((completed * 100) / total));
        }
        if (phase === 'scanning directories') {
          // The progress of the directory scan counts the files found.
          progress.fileCount = completed;
        }
        if (state.kind === 'loading' && state.progress &&
            state.progress.phase === progress.phase && state.progress.percent === progress.percent &&
            state.progress.fileCount === progress.fileCount) {
          return undefined; // Same display
        }
        return { kind: 'loading', progress };
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
      const percent = p?.percent !== undefined ? ` ${p.percent}%` : '';
      return {
        text: `$(sync~spin) VsChromium: indexing${percent}`,
        tooltip: `VsChromium: ${p?.phase ?? 'loading files'}${percent ? ` (${percent.trim()})` : ''}${p?.fileCount !== undefined ? ` (${p.fileCount.toLocaleString('en-US')} files found)` : ''}...`,
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
