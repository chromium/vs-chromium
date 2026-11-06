// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Bench.Shared.Corpus;

/// <summary>
/// Parameters of a generated code base. Two specs with the same values (and the same generator
/// version) always produce exactly the same files.
/// </summary>
public sealed record CorpusSpec(string Name, int Seed, int FileCount) {
  /// <summary>Development size: generated in seconds.</summary>
  public static readonly CorpusSpec Small = new("small", Seed: 1, FileCount: 3_000);
  public static readonly CorpusSpec Medium = new("medium", Seed: 1, FileCount: 40_000);
  /// <summary>Chromium scale (~400k files, several GB).</summary>
  public static readonly CorpusSpec Large = new("large", Seed: 1, FileCount: 400_000);

  public static CorpusSpec FromName(string name) => name.ToLowerInvariant() switch {
    "small" => Small,
    "medium" => Medium,
    "large" => Large,
    _ => throw new ArgumentException($"Unknown corpus size \"{name}\" (expected small, medium or large).", nameof(name)),
  };

  /// <summary>Name of the directory of this corpus (includes the generator version).</summary>
  public string DirectoryName => $"{Name}-{Seed}-{FileCount}-v{CodeBaseGenerator.Version}";
}

/// <summary>A string planted in the generated code base, with the exact number of expected matches.</summary>
public sealed record PlantedMarker(string Text, int FileCount, int HitCount);

/// <summary>
/// Written (last) at the root of a generated corpus. Its presence, with matching values, means the
/// corpus is complete and can be reused.
/// </summary>
public sealed record CorpusManifest(
  int GeneratorVersion,
  CorpusSpec Spec,
  int FileCount,
  int DirectoryCount,
  long TotalBytes,
  int SearchableFileCount,
  DateTime CreatedUtc,
  List<PlantedMarker> Markers);
