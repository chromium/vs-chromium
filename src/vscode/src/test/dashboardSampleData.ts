// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Sample protocol data for the tests of the server dashboard (and to render the page in a browser
// during development).

import type { IndexDetails, IndexStatistics, ProjectDetails, ServerStatistics } from '../protocol/protocol.g';

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
      { name: 'FileSystemScan', count: 3, lastMs: 15_000, totalMs: 48_000, maxMs: 16_800 },
      { name: 'FileDatabaseUpdate', count: 12, lastMs: 4_900, totalMs: 70_000, maxMs: 49_000 },
      { name: 'SearchCode', count: 87, lastMs: 250, totalMs: 21_750, maxMs: 570 },
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
    ...overrides,
  };
}

export function sampleIndexDetails(): IndexDetails {
  return { projects: [sampleProjectDetails('C:\\src\\chromium\\src')] };
}
