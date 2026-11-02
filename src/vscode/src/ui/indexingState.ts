// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import { TypedEvent } from '../server/messages';

/** Indexing state displayed to the user, derived from server events. */
export type IndexingState =
  | { kind: 'starting' }
  | { kind: 'scanning' }
  | { kind: 'loading'; progress?: { text?: string; completed: number; total: number } }
  | { kind: 'ready'; fileCount?: number; projectCount?: number }
  | { kind: 'noProject' }
  | { kind: 'error'; message: string }
  | { kind: 'stopped'; message: string };

/**
 * Computes the next indexing state from a server event. Returns `undefined` if the event does not
 * change the state. Kept free of VS Code dependencies so it can be unit tested.
 */
export function nextIndexingState(state: IndexingState, event: TypedEvent): IndexingState | undefined {
  switch (event.$type) {
    case 'FileSystemScanStarted':
      return { kind: 'scanning' };
    case 'FileSystemScanFinished':
    case 'SearchEngineFilesLoading':
      if ('error' in event && event.error) {
        return { kind: 'error', message: event.error.message ?? 'Error indexing files' };
      }
      return { kind: 'loading' };
    case 'SearchEngineFilesLoadingProgress':
      return state.kind === 'loading' ? undefined : { kind: 'loading' };
    case 'ProgressReportEvent':
      if (state.kind === 'scanning' || state.kind === 'loading') {
        return {
          kind: 'loading',
          progress: { text: event.displayText, completed: event.completed, total: event.total },
        };
      }
      return undefined;
    case 'SearchEngineFilesLoaded':
      if (event.error) {
        return { kind: 'error', message: event.error.message ?? 'Error loading files' };
      }
      return { kind: 'ready' };
    default:
      return undefined;
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
