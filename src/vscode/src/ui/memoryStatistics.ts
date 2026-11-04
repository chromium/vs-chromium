// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import { IndexStatistics } from '../protocol/protocol.g';

const mb = (bytes: number) => `${(bytes / (1024 * 1024)).toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} MB`;

/**
 * The lines of the status dialog describing the memory of the server and of the indexed text:
 * - the text of the files up to 512 KB is stored in arenas (16 MB blocks shared by many files);
 *   their dead bytes are the text of files edited or deleted since the arenas were filled, and their
 *   free bytes the end of the current arena and the ends of full arenas (too small for the file
 *   that didn't fit);
 * - the text of larger files is stored in a block per file.
 * While indexing, the files being read are not in the index yet: the dead bytes are not shown.
 */
export function describeMemory(stats: IndexStatistics, indexing = false): string[] {
  const arenas = stats.fileContentsArenas;
  const lines = [
    `Memory: ${mb(stats.serverGcMemoryUsage)} managed, ${mb(stats.serverNativeMemoryUsage)} native`,
    `Indexed text: ${mb(stats.indexedTextBytes)} (${mb(arenas.liveBytes)} in arenas, ${mb(arenas.largeFileBytes)} in files > 512 KB)`,
  ];
  const memory = `Arenas: ${arenas.arenaCount} (${mb(arenas.arenaBytes)})`;
  if (indexing) {
    lines.push(`${memory}, dead bytes shown when indexing is done`);
    return lines;
  }
  const dead = arenas.usedBytes - arenas.liveBytes;
  const free = arenas.arenaBytes - arenas.usedBytes;
  const sparse = arenas.sparseArenaCount > 0 ? `, ${arenas.sparseArenaCount} arenas more than half dead` : '';
  lines.push(`${memory}: ${mb(arenas.liveBytes)} in use, ${mb(dead)} dead, ${mb(free)} free${sparse}`);
  return lines;
}
