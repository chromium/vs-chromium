// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Sample protocol data for the tests of the server dashboard (and to render the page in a browser
// during development).

import type {
  IndexDetails,
  IndexingStatus,
  IndexStatistics,
  IndexStatus,
  ProjectDetails,
  ServerHistory,
  ServerStatistics,
} from '../protocol/protocol.g';

const MB = 1024 * 1024;
const GB = 1024 * MB;

export function sampleIndexStatistics(overrides: Partial<IndexStatistics> = {}): IndexStatistics {
  return {
    projectCount: 1,
    fileCount: 2_653_112,
    searchableFileCount: 2_176_934,
    serverNativeMemoryUsage: 4.7 * GB,
    serverGcMemoryUsage: 523 * MB,
    indexedTextBytes: 14 * GB,
    fileContentsArenas: {
      arenaCount: 586,
      arenaBytes: 586 * 16 * MB,
      usedBytes: 586 * 16 * MB - 33 * MB,
      liveBytes: 586 * 16 * MB - 33 * MB,
      sparseArenaCount: 0,
      largeFileBytes: 512 * MB,
      cachedFileBytes: 9.3 * GB,
    },
    indexLastUpdatedUtc: '2026-11-04T22:42:13Z',
    status: 'idle',
    indexCache: {
      enabled: true,
      saving: false,
      lastSavedUtc: '2026-11-04T22:42:00Z',
      sizeOnDiskBytes: 9.4 * GB,
      arenaFileCount: 586,
      arenaBytes: 9.3 * GB,
      deadBytes: 120 * 1024 * 1024,
      dirty: false,
    },
    ...overrides,
  };
}

export function sampleServerStatistics(overrides: Partial<ServerStatistics> = {}): ServerStatistics {
  return {
    runtimeDescription: '.NET 10.0.0',
    processId: 4242,
    processorCount: 16,
    uptimeMs: (2 * 3600 + 13 * 60) * 1000,
    cpuTotalMs: 1_234_000,
    cpuUserMs: 1_100_000,
    cpuKernelMs: 134_000,
    workingSetBytes: 6.1 * GB,
    peakWorkingSetBytes: 7.2 * GB,
    privateBytes: 5.4 * GB,
    threadCount: 42,
    handleCount: 1_234,
    gcHeapBytes: 523 * MB,
    gcTotalAllocatedBytes: 48 * GB,
    gcGen0Collections: 1_200,
    gcGen1Collections: 300,
    gcGen2Collections: 12,
    gcTotalPauseMs: 3_400,
    nativeAllocatedBytes: 4.7 * GB,
    indexedFileContentsBytes: 14 * GB,
    trigramFiltersBytes: 0,
    fileContentsArenas: sampleIndexStatistics().fileContentsArenas,
    fileCount: 2_653_112,
    searchableFileCount: 2_176_934,
    projectCount: 1,
    indexCache: {
      enabled: true,
      cacheDirectory: 'C:\\Users\\me\\AppData\\Local\\VsChromium\\IndexCache',
      projectCount: 1,
      readOnlyProjectCount: 0,
      lastLoadFilesFromCache: 2_227_630,
      lastLoadFilesFromDisk: 42_326,
      writeCount: 3,
      lastWriteMs: 4_900,
      lastWriteBytes: 120 * MB,
      lastWriteEntryCount: 2_176_934,
      lastWriteNewFileCount: 1_204,
      lastWriteChangedFileCount: 0,
    },
    operationTimings: [
      { name: 'FileSystemScan', count: 3, lastMs: 15_000, totalMs: 48_000, maxMs: 16_800, recentMs: [16_800, 16_200, 15_000], recentEndUnixMs: [1_780_000_000_000, 1_780_000_600_000, 1_780_001_200_000] },
      { name: 'FileDatabaseUpdate', count: 12, lastMs: 4_900, totalMs: 70_000, maxMs: 49_000, recentMs: [49_000, 4_900], recentEndUnixMs: [1_780_000_100_000, 1_780_001_100_000] },
      { name: 'SearchCode', count: 87, lastMs: 250, totalMs: 21_750, maxMs: 570, recentMs: [250], recentEndUnixMs: [1_780_001_150_000] },
    ],
    ...overrides,
  };
}

export function sampleProjectDetails(rootPath: string, overrides: Partial<ProjectDetails> = {}): ProjectDetails {
  const section = (name: string) => ({ containingFilePath: `${rootPath}\\vs-chromium-project.txt`, name, contents: '' });
  return {
    rootPath,
    directoryDetails: {
      path: rootPath,
      directoryCount: 312_000,
      fileCount: 2_653_112,
      searchableFilesCount: 2_176_934,
      searchableFilesByteLength: 14 * GB,
      binaryFilesCount: 476_178,
      binaryFilesByteLength: 21 * GB,
      searchableFilesByExtensionDetails: [],
      largeSearchableFilesDetails: [],
      binaryFilesByExtensionDetails: [],
      largeBinaryFilesDetails: [],
    },
    configuration: {
      ignorePathsSection: section('Ignore'),
      ignoreSearchableFilesSection: section('SearchableFiles.ignore'),
      includeSearchableFilesSection: section('SearchableFiles.include'),
    },
    trigramFiltersEnabled: false,
    trigramFiltersBytes: 0,
    memory: {
      nativeBytes: 4.6 * GB, arenaCount: 290, arenaBytes: 4.53 * GB, liveBytes: 4.5 * GB, deadBytes: 0, freeBytes: 30 * MB,
      largeFileBytes: 70 * MB, mappedCacheBytes: 9.3 * GB, trigramFiltersBytes: 0,
    },
    cache: {
      enabled: true, directory: `C:\\Users\\me\\AppData\\Local\\VsChromium\\Cache\\3f2a0b1c2d3e4f50`, writable: true,
      sizeOnDiskBytes: 9.4 * GB, deadBytes: 120 * MB, lastSavedUtc: '2026-11-04T22:42:13Z', dirty: false, saving: false,
    },
    operations: {
      initialLoad: { count: 1, minMs: 66_000, avgMs: 66_000, maxMs: 66_000, lastMs: 66_000, scanMs: 15_000, fromCache: true, cachedIndexMs: 4_900 },
      incrementalUpdates: { count: 48, minMs: 40, avgMs: 300, maxMs: 2_100, lastMs: 120 },
      lastVerification: {
        method: 'changeJournal', durationMs: 420, journalRecordCount: 52_000, listedDirectoryCount: 312, scannedDirectoryCount: 3,
        symLinkDirectoryCount: 0, symLinkFileCount: 42_800,
      },
    },    ...overrides,
  };
}

export function sampleIndexDetails(): IndexDetails {
  return { projects: [sampleProjectDetails('C:\\src\\chromium\\src')] };
}

/** The status part of `sampleIndexStatistics` ("index/status"). */
export function sampleIndexStatus(overrides: Partial<IndexStatus> = {}): IndexStatus {
  const s = sampleIndexStatistics();
  return {
    status: s.status,
    indexLastUpdatedUtc: s.indexLastUpdatedUtc,
    projectCount: s.projectCount,
    fileCount: s.fileCount,
    searchableFileCount: s.searchableFileCount,
    indexedTextBytes: s.indexedTextBytes,
    serverNativeMemoryUsage: s.serverNativeMemoryUsage,
    serverGcMemoryUsage: s.serverGcMemoryUsage,
    indexCache: s.indexCache,
    ...overrides,
  };
}

/** A history built from explicit samples (tests). */
export function historyOf(samples: { time: number; cpuTotalMs?: number; gcHeapBytes?: number; status?: IndexingStatus }[]): ServerHistory {
  return {
    intervalMs: 10_000,
    capacity: 720,
    timesUnixMs: samples.map(s => s.time),
    gcHeapBytes: samples.map(s => s.gcHeapBytes ?? 100 * MB),
    gcTotalAllocatedBytes: samples.map((_, i) => i * 10 * MB),
    nativeAllocatedBytes: samples.map(() => 4 * GB),
    workingSetBytes: samples.map(() => 6 * GB),
    privateBytes: samples.map(() => 5 * GB),
    cpuTotalMs: samples.map((s, i) => s.cpuTotalMs ?? i * 1000),
    fileCount: samples.map(() => 2_653_112),
    searchableFileCount: samples.map(() => 2_176_934),
    indexedTextBytes: samples.map(() => 14 * GB),
    cacheSizeOnDiskBytes: samples.map(() => 9.4 * GB),
    status: samples.map(s => s.status ?? 'idle'),
  };
}

/**
 * Two hours of synthetic samples (every 10 s, the last one at `endMs`) of a server that starts,
 * indexes, verifies and idles, with a pause: for the screenshots of the charts.
 */
export function syntheticHistory(endMs: number, count = 720): ServerHistory {
  const times: number[] = [];
  const statuses: IndexingStatus[] = [];
  const gc: number[] = [];
  const native: number[] = [];
  const workingSet: number[] = [];
  const privateBytes: number[] = [];
  const cpu: number[] = [];
  const allocated: number[] = [];
  const cache: number[] = [];
  let cpuMs = 0;
  let allocatedBytes = 0;
  for (let i = 0; i < count; i++) {
    const t = endMs - (count - 1 - i) * 10_000;
    const phase = i / count;
    const status: IndexingStatus = phase < 0.12 ? 'busy' : phase < 0.2 ? 'verifying' : phase > 0.55 && phase < 0.6 ? 'paused' : phase > 0.8 && phase < 0.84 ? 'busy' : 'idle';
    const loading = Math.min(1, phase / 0.12);
    const wave = Math.sin(i / 9) * 0.5 + 0.5;
    const busy = status === 'busy' || status === 'verifying';
    const gcBytes = (busy ? 900 : 480) * MB + wave * 120 * MB;
    const nativeBytes = 4.2 * GB * loading + (phase > 0.8 ? 0.3 * GB : 0);
    cpuMs += 10_000 * 16 * (busy ? 0.35 + wave * 0.2 : 0.01 + wave * 0.02);
    allocatedBytes += (busy ? 300 : 20) * MB;
    times.push(t);
    statuses.push(status);
    gc.push(gcBytes);
    native.push(nativeBytes);
    workingSet.push(nativeBytes + gcBytes + 0.9 * GB);
    privateBytes.push(nativeBytes + gcBytes + 0.4 * GB);
    cpu.push(cpuMs);
    allocated.push(allocatedBytes);
    cache.push(phase < 0.15 ? 0 : phase < 0.85 ? 9.2 * GB : 9.4 * GB);
  }
  return {
    intervalMs: 10_000,
    capacity: 720,
    timesUnixMs: times,
    gcHeapBytes: gc,
    gcTotalAllocatedBytes: allocated,
    nativeAllocatedBytes: native,
    workingSetBytes: workingSet,
    privateBytes,
    cpuTotalMs: cpu,
    fileCount: times.map(() => 2_653_112),
    searchableFileCount: times.map(() => 2_176_934),
    indexedTextBytes: times.map(() => 14 * GB),
    cacheSizeOnDiskBytes: cache,
    status: statuses,
  };
}
