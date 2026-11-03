// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { describeIndexingState, IndexEvent, IndexingState, nextIndexingState } from '../ui/indexingState';

const step = (state: IndexingState, event: IndexEvent): IndexingState => nextIndexingState(state, event) ?? state;

describe('nextIndexingState', () => {
  it('follows the scan / load / ready sequence', () => {
    let state = step({ kind: 'starting' }, { type: 'scanStarted', params: { operationId: 1 } });
    assert.equal(state.kind, 'scanning');

    state = step(state, { type: 'scanFinished', params: { operationId: 1, oldVersion: 0, newVersion: 1 } });
    assert.equal(state.kind, 'loading');

    state = step(state, { type: 'progress', params: { displayText: 'Loading files', completed: 5, total: 10 } });
    assert.deepEqual(state, { kind: 'loading', progress: { text: 'Loading files', completed: 5, total: 10 } });
    assert.match(describeIndexingState(state).text, /50%/);

    state = step(state, { type: 'loadingProgress', params: { operationId: 2 } });
    assert.equal(state.kind, 'loading');

    state = step(state, { type: 'loaded', params: { operationId: 2, treeVersion: 1 } });
    assert.equal(state.kind, 'ready');
  });

  it('enters the loading state on index/loading', () => {
    assert.deepEqual(nextIndexingState({ kind: 'ready' }, { type: 'loading', params: { operationId: 1 } }), { kind: 'loading' });
  });

  it('ignores progress notifications when not indexing', () => {
    const ready: IndexingState = { kind: 'ready' };
    assert.equal(nextIndexingState(ready, { type: 'progress', params: { displayText: '', completed: 1, total: 2 } }), undefined);
  });

  it('reports errors', () => {
    const next = nextIndexingState({ kind: 'loading' }, {
      type: 'loaded',
      params: { operationId: 3, treeVersion: 1, error: { message: 'Out of memory' } },
    });
    assert.deepEqual(next, { kind: 'error', message: 'Out of memory' });

    const scan = nextIndexingState({ kind: 'scanning' }, {
      type: 'scanFinished',
      params: { operationId: 1, oldVersion: 0, newVersion: 0, error: { message: 'Access denied' } },
    });
    assert.deepEqual(scan, { kind: 'error', message: 'Access denied' });
  });

  it('tracks pause and resume (index/stateChanged)', () => {
    const at = '2026-01-01T00:00:00Z';
    let state = step({ kind: 'ready' }, { type: 'stateChanged', params: { status: 'paused', lastIndexUpdatedUtc: at } });
    assert.deepEqual(state, { kind: 'paused', yielded: false });
    assert.match(describeIndexingState(state).tooltip, /paused/);

    state = step(state, { type: 'stateChanged', params: { status: 'idle', lastIndexUpdatedUtc: at } });
    assert.equal(state.kind, 'ready');

    state = step(state, { type: 'stateChanged', params: { status: 'yield', lastIndexUpdatedUtc: at } });
    assert.deepEqual(state, { kind: 'paused', yielded: true });

    // "busy" and "idle" outside of a pause: the scan / load notifications drive the state.
    assert.equal(nextIndexingState({ kind: 'scanning' }, { type: 'stateChanged', params: { status: 'busy', lastIndexUpdatedUtc: at } }), undefined);
    assert.equal(nextIndexingState({ kind: 'loading' }, { type: 'stateChanged', params: { status: 'idle', lastIndexUpdatedUtc: at } }), undefined);
  });
});
