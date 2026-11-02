// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { describeIndexingState, IndexingState, nextIndexingState } from '../ui/indexingState';

describe('nextIndexingState', () => {
  it('follows the scan / load / ready sequence', () => {
    type Event = Parameters<typeof nextIndexingState>[1];
    const step = (state: IndexingState, event: Event): IndexingState => nextIndexingState(state, event) ?? state;

    let state = step({ kind: 'starting' }, { $type: 'FileSystemScanStarted', operationId: 1 });
    assert.equal(state.kind, 'scanning');

    state = step(state, { $type: 'FileSystemScanFinished', operationId: 1, oldVersion: 0, newVersion: 1 } as Event);
    assert.equal(state.kind, 'loading');

    state = step(state, { $type: 'ProgressReportEvent', displayText: 'Loading files', completed: 5, total: 10 });
    assert.deepEqual(state, { kind: 'loading', progress: { text: 'Loading files', completed: 5, total: 10 } });
    assert.match(describeIndexingState(state).text, /50%/);

    state = step(state, { $type: 'SearchEngineFilesLoaded', operationId: 2 });
    assert.equal(state.kind, 'ready');
  });

  it('ignores progress events when not indexing', () => {
    const ready: IndexingState = { kind: 'ready' };
    assert.equal(nextIndexingState(ready, { $type: 'ProgressReportEvent', completed: 1, total: 2 }), undefined);
  });

  it('reports errors', () => {
    const next = nextIndexingState({ kind: 'loading' }, {
      $type: 'SearchEngineFilesLoaded',
      operationId: 3,
      error: { $type: 'ErrorResponse', message: 'Out of memory' },
    });
    assert.deepEqual(next, { kind: 'error', message: 'Out of memory' });
  });
});
