// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { IndexCacheState } from '../protocol/protocol.g';
import { decorateIndexingDisplay, describeIndexCache, shouldPollIndexCache } from '../ui/indexCacheStatus';

const MB = 1024 * 1024;
const time = (utc: string) => `time(${utc})`;

function cache(state: Partial<IndexCacheState>): IndexCacheState {
  return { enabled: true, saving: false, sizeOnDiskBytes: 9_412 * MB, dirty: false, ...state };
}

describe('describeIndexCache', () => {
  it('shows the size on disk and the time of the last save', () => {
    assert.equal(describeIndexCache(cache({ lastSavedUtc: '2026-01-01T22:42:13Z' }), time),
      'Index cache: 9,412 MB on disk, saved time(2026-01-01T22:42:13Z)');
  });

  it('shows a save in progress and a changed index', () => {
    assert.equal(describeIndexCache(cache({ lastSavedUtc: 'T', saving: true, dirty: true }), time),
      'Index cache: 9,412 MB on disk, saved time(T) (saving\u2026)');
    assert.equal(describeIndexCache(cache({ lastSavedUtc: 'T', dirty: true }), time),
      'Index cache: 9,412 MB on disk, saved time(T), index changed since');
  });

  it('shows a cache not saved yet, or disabled', () => {
    assert.equal(describeIndexCache(cache({}), time), 'Index cache: not saved yet');
    assert.equal(describeIndexCache(cache({ saving: true }), time), 'Index cache: not saved yet (saving\u2026)');
    assert.equal(describeIndexCache(cache({ enabled: false }), time), 'Index cache: disabled');
    assert.equal(describeIndexCache(undefined, time), 'Index cache: disabled');
  });
});

describe('decorateIndexingDisplay', () => {
  const display = { text: '$(database) VsChromium', tooltip: 'VsChromium: index ready.' };

  it('adds a spinner while saving, a dot when the index changed since the last save', () => {
    assert.equal(decorateIndexingDisplay(display, { kind: 'ready' }, cache({ saving: true, dirty: true })).text, '$(database) VsChromium $(sync~spin)');
    const dirty = decorateIndexingDisplay(display, { kind: 'ready' }, cache({ dirty: true }));
    assert.equal(dirty.text, '$(database) VsChromium $(circle-filled)');
    assert.match(dirty.tooltip, /changed since the last save/);
  });

  it('adds nothing when saved, disabled, or not ready', () => {
    assert.deepEqual(decorateIndexingDisplay(display, { kind: 'ready' }, cache({})), display);
    assert.deepEqual(decorateIndexingDisplay(display, { kind: 'ready' }, cache({ enabled: false, dirty: true })), display);
    assert.deepEqual(decorateIndexingDisplay(display, { kind: 'ready' }, undefined), display);
    assert.deepEqual(decorateIndexingDisplay(display, { kind: 'verifying' }, cache({ dirty: true })), display);
  });

  it('polls only while saving', () => {
    assert.equal(shouldPollIndexCache(cache({ saving: true })), true);
    assert.equal(shouldPollIndexCache(cache({ dirty: true })), false);
    assert.equal(shouldPollIndexCache(undefined), false);
  });
});
