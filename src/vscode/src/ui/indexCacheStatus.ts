// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import { IndexCacheState } from '../protocol/protocol.g';
import { IndexingState } from './indexingState';

/** Interval of the requests of the state of the index cache while it is being saved. */
export const IndexCacheSavingPollIntervalMs = 2000;

const mb = (bytes: number) => `${Math.round(bytes / (1024 * 1024)).toLocaleString('en-US')} MB`;

/**
 * The line of the status dialog describing the index cache (the index saved on disk, used to load
 * the index quickly when the server restarts).
 */
export function describeIndexCache(
  cache: IndexCacheState | undefined,
  formatTime: (utc: string) => string = utc => new Date(utc).toLocaleTimeString(),
): string {
  if (!cache || !cache.enabled) {
    return 'Index cache: disabled';
  }
  const saving = cache.saving ? ' (saving\u2026)' : '';
  if (!cache.lastSavedUtc) {
    return `Index cache: not saved yet${saving}`;
  }
  const changed = cache.dirty && !cache.saving ? ', index changed since' : '';
  // Dead bytes: files modified or deleted since they were saved (removed by the compaction).
  const dead = cache.deadBytes > 0 ? ` (${mb(cache.deadBytes)} dead)` : '';
  return `Index cache: ${mb(cache.sizeOnDiskBytes)} on disk${dead}, saved ${formatTime(cache.lastSavedUtc)}${changed}${saving}`;
}

/**
 * The status bar display of a ready index, with the state of the index cache: a spinner while it
 * is being saved, a dot if the index changed since it was last saved, nothing otherwise.
 */
export function decorateIndexingDisplay(
  display: { text: string; tooltip: string },
  state: IndexingState,
  cache: IndexCacheState | undefined,
): { text: string; tooltip: string } {
  if (state.kind !== 'ready' || !cache || !cache.enabled) {
    return display;
  }
  if (cache.saving) {
    return { text: `${display.text} $(sync~spin)`, tooltip: `${display.tooltip}\nSaving the index cache...` };
  }
  if (cache.dirty) {
    return { text: `${display.text} $(circle-filled)`, tooltip: `${display.tooltip}\nThe index changed since the last save of the index cache.` };
  }
  return display;
}

/** True if the state of the index cache must be requested again soon (a save is in progress). */
export function shouldPollIndexCache(cache: IndexCacheState | undefined): boolean {
  return cache?.saving === true;
}
