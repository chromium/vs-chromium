// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VsChromium.Bench.Shared.Corpus;

/// <summary>A generated code base on disk.</summary>
public sealed record GeneratedCorpus(string RootPath, CorpusManifest Manifest) {
  public PlantedMarker Marker(string text) => Manifest.Markers.Single(m => m.Text == text);
}

/// <summary>
/// Gives access to generated code bases, which are **kept between test runs** (never deleted by the
/// tests) in a directory given by the caller (e.g. the corpora directory of the test settings), so
/// that running tests over and over does not delete/re-create hundreds of thousands of files.
///
/// A corpus is generated in a temporary directory, which is then renamed to its final name; the
/// manifest is written last, <b>next to</b> the corpus directory (<c>&lt;dir&gt;.manifest.json</c>,
/// outside of it so that it is not indexed by the server). A corpus directory with a matching
/// manifest is reused as is; a corpus directory without (or with a non-matching) manifest, e.g. after
/// an interrupted generation, is deleted and re-generated.
/// </summary>
public static class CorpusCache {
  /// <summary>Returns the corpus <paramref name="spec"/> of the directory <paramref name="corporaDir"/>, generated if needed.</summary>
  public static GeneratedCorpus GetOrCreate(string corporaDir, CorpusSpec spec, Action<string>? log = null) {
    log ??= _ => { };
    Directory.CreateDirectory(corporaDir);
    var finalDir = Path.Combine(corporaDir, spec.DirectoryName);
    var manifestPath = ManifestPath(finalDir);

    // Serialize generation across test processes (e.g. parallel test runs).
    using var lockFile = AcquireLock(Path.Combine(corporaDir, spec.DirectoryName + ".lock"));

    var existing = TryReadManifest(manifestPath);
    if (existing != null && existing.GeneratorVersion == CodeBaseGenerator.Version && existing.Spec == spec && Directory.Exists(finalDir)) {
      log($"Reusing generated code base \"{finalDir}\" ({existing.FileCount:n0} files, {existing.TotalBytes:n0} bytes).");
      return new GeneratedCorpus(finalDir, existing);
    }

    log($"Generating code base \"{finalDir}\" ({spec.FileCount:n0} files)...");
    var sw = Stopwatch.StartNew();
    var tempDir = PrepareDirectories(finalDir, manifestPath);
    var manifest = new CodeBaseGenerator(spec).Generate(tempDir);
    Directory.Move(tempDir, finalDir);
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, CorpusJsonContext.Default.CorpusManifest));
    log($"Generated {manifest.FileCount:n0} files ({manifest.TotalBytes:n0} bytes) in {sw.Elapsed.TotalSeconds:n1} s.");
    return new GeneratedCorpus(finalDir, manifest);
  }

  /// <summary>
  /// Same caching scheme for small hand-written corpora: <paramref name="generate"/> writes the files
  /// into the given (empty) directory. Increment <paramref name="version"/> when the files change.
  /// </summary>
  public static string GetOrCreateCustom(string corporaDir, string name, int version, Action<string> generate) {
    Directory.CreateDirectory(corporaDir);
    var dirName = $"{name}-v{version}";
    var finalDir = Path.Combine(corporaDir, dirName);
    var manifestPath = ManifestPath(finalDir);
    using var lockFile = AcquireLock(Path.Combine(corporaDir, dirName + ".lock"));
    if (File.Exists(manifestPath) && Directory.Exists(finalDir)) {
      return finalDir;
    }
    var tempDir = PrepareDirectories(finalDir, manifestPath);
    generate(tempDir);
    Directory.Move(tempDir, finalDir);
    File.WriteAllText(manifestPath, new JsonObject { ["name"] = name, ["version"] = version }.ToJsonString());
    return finalDir;
  }

  private static string ManifestPath(string corpusDir) => corpusDir + ".manifest.json";

  /// <summary>Deletes leftovers of a previous (incomplete or outdated) corpus, returns an empty temp directory.</summary>
  private static string PrepareDirectories(string finalDir, string manifestPath) {
    File.Delete(manifestPath);
    if (Directory.Exists(finalDir)) {
      Directory.Delete(finalDir, recursive: true);
    }
    var tempDir = finalDir + ".tmp-" + Environment.ProcessId;
    if (Directory.Exists(tempDir)) {
      Directory.Delete(tempDir, recursive: true);
    }
    Directory.CreateDirectory(tempDir);
    return tempDir;
  }

  private static CorpusManifest? TryReadManifest(string path) {
    if (!File.Exists(path)) {
      return null;
    }
    try {
      return JsonSerializer.Deserialize(File.ReadAllText(path), CorpusJsonContext.Default.CorpusManifest);
    } catch (JsonException) {
      return null;
    }
  }

  private static FileStream AcquireLock(string path) {
    var deadline = DateTime.UtcNow + TimeSpan.FromHours(1);
    while (true) {
      try {
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
      } catch (IOException) when (DateTime.UtcNow < deadline) {
        Thread.Sleep(500);
      }
    }
  }
}

/// <summary>Serialization of the manifests (source generated: the benchmark app is published with NativeAOT).</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CorpusManifest))]
internal sealed partial class CorpusJsonContext : JsonSerializerContext;