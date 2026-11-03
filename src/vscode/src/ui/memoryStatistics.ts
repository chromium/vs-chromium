// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import { FileContentsArenaStatistics } from '../protocol/protocol.g';

const mb = (bytes: number) => `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

/**
 * The line of the status dialog describing the arenas storing the text of the indexed files: their
 * memory, and the dead bytes (text of files edited or deleted since the arenas were filled). While
 * indexing, the files being read are not in the index yet: the dead bytes are not shown.
 */
export function describeArenas(arenas: FileContentsArenaStatistics, indexing = false): string {
  const memory = `File contents: ${arenas.arenaCount} arenas (${mb(arenas.arenaBytes)})`;
  if (indexing) {
    return `${memory}, dead bytes shown when indexing is done`;
  }
  const dead = arenas.usedBytes - arenas.liveBytes;
  const percent = arenas.usedBytes > 0 ? Math.round((dead * 100) / arenas.usedBytes) : 0;
  const sparse = arenas.sparseArenaCount > 0 ? `, ${arenas.sparseArenaCount} more than half dead` : '';
  return `${memory}, ${mb(dead)} dead (${percent}%)${sparse}`;
}
