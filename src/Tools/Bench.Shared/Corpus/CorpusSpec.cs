// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Bench.Shared.Corpus;

/// <summary>
/// Parameters of a generated code base. Two specs with the same values (and the same generator
/// version) always produce exactly the same files. <paramref name="Symlinks"/>: the same corpus plus
/// symbolic links (a separate directory).
/// </summary>
public sealed record CorpusSpec(string Name, int Seed, int FileCount, bool Symlinks = false) {
  /// <summary>Development size: generated in seconds.</summary>
  public static readonly CorpusSpec Small = new("small", Seed: 1, FileCount: 3_000);
  public static readonly CorpusSpec Medium = new("medium", Seed: 1, FileCount: 40_000);
  /// <summary>Chromium scale (~400k files, several GB).</summary>
  public static readonly CorpusSpec Large = new("large", Seed: 1, FileCount: 400_000);
  /// <summary>1M files (benchmarks of large projects).</summary>
  public static readonly CorpusSpec XLarge = new("xlarge", Seed: 1, FileCount: 1_000_000);
  /// <summary>2M files (benchmarks of very large projects).</summary>
  public static readonly CorpusSpec XXLarge = new("xxlarge", Seed: 1, FileCount: 2_000_000);

  public static IReadOnlyList<CorpusSpec> All { get; } = [Small, Medium, Large, XLarge, XXLarge];

  /// <summary>A spec from its name ("small", "medium", "large", "xlarge", "xxlarge") or size ("400k", "1M", "2M").</summary>
  public static CorpusSpec FromName(string name) => name.ToLowerInvariant() switch {
    "small" => Small,
    "medium" => Medium,
    "large" or "400k" => Large,
    "xlarge" or "1m" => XLarge,
    "xxlarge" or "2m" => XXLarge,
    _ => throw new ArgumentException($"Unknown corpus size \"{name}\" (expected small, medium, large (400k), xlarge (1M) or xxlarge (2M)).", nameof(name)),
  };

  /// <summary>Name of the directory of this corpus (includes the generator version).</summary>
  public string DirectoryName => $"{Name}-{Seed}-{FileCount}{(Symlinks ? "-symlinks" : "")}-v{CodeBaseGenerator.Version}";
}

/// <summary>A string planted in the generated code base, with the exact number of expected matches.</summary>
public sealed record PlantedMarker(string Text, int FileCount, int HitCount);

/// <summary>
/// A bucket of the distribution of the number of files per directory (e.g. "2-4": the directories
/// with 2 to 4 files), with the percentages of the directories and of the files.
/// </summary>
public sealed record FilesPerDirectoryBucket(string Bucket, int DirectoryCount, int FileCount, double DirectoryPercent, double FilePercent);

/// <summary>
/// Written (last) next to a generated corpus. Its presence, with matching values, means the corpus
/// is complete and can be reused.
/// </summary>
public sealed record CorpusManifest(
  int GeneratorVersion,
  CorpusSpec Spec,
  int FileCount,
  int DirectoryCount,
  long TotalBytes,
  int SearchableFileCount,
  DateTime CreatedUtc,
  List<PlantedMarker> Markers) {
  /// <summary>Symbolic links: "none" (not asked), "created", or "skipped (reason)".</summary>
  public string Symlinks { get; init; } = "none";
  public int SymlinkFileCount { get; init; }
  public int SymlinkDirectoryCount { get; init; }
  /// <summary>Files seen through the links (file links and the files of the linked directories).</summary>
  public int FilesThroughSymlinksCount { get; init; }
  /// <summary>Time to generate the corpus, in seconds.</summary>
  public double GenerationSeconds { get; init; }
  /// <summary>Distribution of the number of files per source directory (see CodeBaseGenerator.FilesPerDirectoryBuckets).</summary>
  public List<FilesPerDirectoryBucket>? FilesPerDirectoryHistogram { get; init; }
}
