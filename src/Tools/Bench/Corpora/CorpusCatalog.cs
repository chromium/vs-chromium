// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Bench.Shared.Corpus;

namespace VsChromium.Bench.Corpora;

/// <summary>Disk space needed by a run on a corpus.</summary>
public sealed record DiskEstimate(long CorpusBytes, long CacheBytes, bool CorpusExists) {
  /// <summary>Space needed now: the corpus (unless it exists) and the index cache.</summary>
  public long NeededBytes => (CorpusExists ? 0 : CorpusBytes) + CacheBytes;

  /// <summary>Space needed with the safety margin (+10%).</summary>
  public long RequiredFreeBytes => (long)(NeededBytes * 1.1);

  public bool Fits(long freeBytes) => freeBytes >= RequiredFreeBytes;
}

/// <summary>
/// The corpora offered by the bench (400k, 1M, 2M; small and medium for quick tests) and their disk
/// estimates, from the average sizes of the generator version 2 (measured on the 400k corpus:
/// 3,912,356,729 bytes, 86,864 directories).
/// </summary>
public static class CorpusCatalog {
  /// <summary>Average size of a file (log-normal distribution, very large files excluded).</summary>
  public const long AverageFileBytes = 9_300;

  /// <summary>Average size of the very large files (2-16 MB).</summary>
  public const long AverageVeryLargeFileBytes = 9L << 20;

  /// <summary>Allocation overhead per file (cluster slack, metadata).</summary>
  public const long AllocationOverheadPerFile = 2_048;

  /// <summary>Index cache size relative to the corpus size (text of the searchable files and index).</summary>
  public const double CacheRatio = 0.95;

  public static readonly IReadOnlyList<string> MenuNames = ["400k", "1M", "2M"];

  public static CorpusSpec Spec(string name, bool symlinks) =>
    CorpusSpec.FromName(name) with { Symlinks = symlinks };

  /// <summary>Estimated bytes of the files of a corpus.</summary>
  public static long EstimateCorpusBytes(int fileCount) =>
    fileCount * AverageFileBytes + CodeBaseGenerator.VeryLargeFileCount * AverageVeryLargeFileBytes;

  /// <summary>Estimated space on disk of a corpus (files and allocation overhead).</summary>
  public static long EstimateCorpusDiskBytes(int fileCount) =>
    EstimateCorpusBytes(fileCount) + fileCount * AllocationOverheadPerFile;

  public static long EstimateCacheBytes(int fileCount) => (long)(EstimateCorpusBytes(fileCount) * CacheRatio);

  public static DiskEstimate Estimate(CorpusSpec spec, bool corpusExists) =>
    new(EstimateCorpusDiskBytes(spec.FileCount), EstimateCacheBytes(spec.FileCount), corpusExists);

  /// <summary>The default directory of the corpora of a disk.</summary>
  public static string DefaultCorporaDirectory(string mountPoint) => Path.Combine(mountPoint, "vschromium-corpora");
}
