// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { describeIndexingState, IndexEvent, IndexingState, loadingPhase, nextIndexingState } from '../ui/indexingState';

const step = (state: IndexingState, event: IndexEvent): IndexingState => nextIndexingState(state, event) ?? state;

describe('nextIndexingState', () => {
  it('follows the scan / load / ready sequence', () => {
    let state = step({ kind: 'starting' }, { type: 'scanStarted', params: { operationId: 1 } });
    assert.equal(state.kind, 'scanning');

    state = step(state, { type: 'scanFinished', params: { operationId: 1, oldVersion: 0, newVersion: 1 } });
    assert.equal(state.kind, 'loading');

    state = step(state, { type: 'progress', params: { displayText: 'Reading file 5 of 10: c:\\src\\foo.cc', completed: 5, total: 10 } });
    assert.deepEqual(state, { kind: 'loading', progress: { phase: 'reading files', percent: 50 } });
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

  it('only changes the loading state when the displayed progress changes', () => {
    // The server sends a progress notification (with the path of the file being read) up to 10
    // times per second: the display must only change with the phase or the whole percentage.
    let state: IndexingState = { kind: 'loading' };
    state = step(state, { type: 'progress', params: { displayText: 'Reading file 1000 of 100000: c:\\a\\b.cc', completed: 1000, total: 100000 } });
    assert.deepEqual(state, { kind: 'loading', progress: { phase: 'reading files', percent: 1 } });
    const tooltip = describeIndexingState(state).tooltip;
    assert.equal(tooltip, 'VsChromium: reading files (1%)...');
    assert.doesNotMatch(tooltip, /b\.cc/);

    assert.equal(nextIndexingState(state, { type: 'progress', params: { displayText: 'Reading file 1500 of 100000: c:\\a\\very\\long\\path\\c.cc', completed: 1500, total: 100000 } }), undefined);
    assert.deepEqual(nextIndexingState(state, { type: 'progress', params: { displayText: 'Reading file 2000 of 100000: c:\\d.cc', completed: 2000, total: 100000 } }),
      { kind: 'loading', progress: { phase: 'reading files', percent: 2 } });
  });

  it('describes the loading phases without the server details', () => {
    const traversing = step({ kind: 'scanning' }, { type: 'progress', params: { displayText: 'Traversing directory: c:\\src\\x', completed: 42, total: 0 } });
    assert.deepEqual(traversing, { kind: 'loading', progress: { phase: 'scanning directories', percent: undefined } });
    assert.equal(describeIndexingState(traversing).tooltip, 'VsChromium: scanning directories...');
    // Directory counts change all the time (indeterminate progress): no new state.
    assert.equal(nextIndexingState(traversing, { type: 'progress', params: { displayText: 'Traversing directory: c:\\src\\y', completed: 43, total: 0 } }), undefined);

    assert.equal(loadingPhase('Finalizing index update'), 'finalizing');
    assert.equal(loadingPhase('Preparing list of files to load from disk'), 'loading files');
    assert.equal(loadingPhase(undefined), 'loading files');
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
