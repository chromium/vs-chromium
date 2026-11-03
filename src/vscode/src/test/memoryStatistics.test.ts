// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { describeArenas } from '../ui/memoryStatistics';

const MB = 1024 * 1024;

describe('describeArenas', () => {
  it('shows the memory and the dead bytes of the arenas', () => {
    assert.equal(
      describeArenas({ arenaCount: 2, arenaBytes: 32 * MB, usedBytes: 20 * MB, liveBytes: 15 * MB, sparseArenaCount: 0 }),
      'File contents: 2 arenas (32.0 MB), 5.0 MB dead (25%)');
  });

  it('shows the arenas more than half dead', () => {
    assert.equal(
      describeArenas({ arenaCount: 3, arenaBytes: 48 * MB, usedBytes: 40 * MB, liveBytes: 10 * MB, sparseArenaCount: 2 }),
      'File contents: 3 arenas (48.0 MB), 30.0 MB dead (75%), 2 more than half dead');
  });

  it('handles empty arenas', () => {
    assert.equal(
      describeArenas({ arenaCount: 0, arenaBytes: 0, usedBytes: 0, liveBytes: 0, sparseArenaCount: 0 }),
      'File contents: 0 arenas (0.0 MB), 0.0 MB dead (0%)');
  });
});
