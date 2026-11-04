// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { IndexStatistics } from '../protocol/protocol.g';
import { describeMemory } from '../ui/memoryStatistics';

const MB = 1024 * 1024;

function statistics(arenas: Partial<IndexStatistics['fileContentsArenas']>): IndexStatistics {
  return {
    projectCount: 1,
    fileCount: 10,
    searchableFileCount: 10,
    serverNativeMemoryUsage: 14_037 * MB,
    serverGcMemoryUsage: 523 * MB,
    indexedTextBytes: 14_004 * MB,
    indexLastUpdatedUtc: '2026-01-01T00:00:00Z',
    status: 'idle',
    fileContentsArenas: {
      arenaCount: 586,
      arenaBytes: 586 * 16 * MB,
      usedBytes: 9_343 * MB,
      liveBytes: 9_343 * MB,
      sparseArenaCount: 0,
      largeFileBytes: 4_661 * MB,
      cachedFileBytes: 0,
      ...arenas,
    },
  };
}

describe('describeMemory', () => {
  it('shows the native memory, the indexed text and the arenas', () => {
    assert.deepEqual(describeMemory(statistics({})), [
      'Memory: 523.0 MB managed, 14,037.0 MB native',
      'Indexed text: 14,004.0 MB (9,343.0 MB in arenas, 4,661.0 MB in files > 512 KB)',
      'Arenas: 586 (9,376.0 MB): 9,343.0 MB in use, 0.0 MB dead, 33.0 MB free',
    ]);
  });

  it('shows the dead bytes and the arenas more than half dead', () => {
    const lines = describeMemory(statistics({ liveBytes: 9_000 * MB, sparseArenaCount: 3 }));
    assert.equal(lines[2], 'Arenas: 586 (9,376.0 MB): 9,000.0 MB in use, 343.0 MB dead, 33.0 MB free, 3 arenas more than half dead');
  });

  it('shows the text loaded from the index cache (memory mapped files)', () => {
    const lines = describeMemory(statistics({ liveBytes: 10 * MB, usedBytes: 10 * MB, cachedFileBytes: 9_333 * MB }));
    assert.equal(lines[1], 'Indexed text: 14,004.0 MB (10.0 MB in arenas, 4,661.0 MB in files > 512 KB, 9,333.0 MB from the cache)');
  });

  it('does not show the dead bytes while indexing (files being read are not in the index yet)', () => {
    const lines = describeMemory(statistics({ liveBytes: 5_000 * MB }), true);
    assert.equal(lines[2], 'Arenas: 586 (9,376.0 MB), dead bytes shown when indexing is done');
  });
});
