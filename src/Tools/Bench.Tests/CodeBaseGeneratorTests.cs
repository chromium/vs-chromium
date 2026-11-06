// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Bench.Shared.Corpus;

namespace VsChromium.Bench.Tests;

[TestClass]
public class CodeBaseGeneratorTests {
  public TestContext TestContext { get; set; } = null!;

  /// <summary>
  /// The distribution of the number of files per directory of the generated corpus matches the
  /// distribution measured on a large real project (within a few percentage points per bucket).
  /// </summary>
  [TestMethod]
  public void FilesPerDirectoryHistogramMatchesTheRealProject() {
    var histogram = new CodeBaseGenerator(CorpusSpec.Medium).ComputeFilesPerDirectoryHistogram();
    foreach (var bucket in histogram) {
      TestContext.WriteLine($"{bucket.Bucket,-10} dirs {bucket.DirectoryCount,7:n0} ({bucket.DirectoryPercent,5:n1}%)  files {bucket.FileCount,7:n0} ({bucket.FilePercent,5:n1}%)");
    }
    var expected = CodeBaseGenerator.FilesPerDirectoryBuckets;
    Assert.HasCount(expected.Count, histogram);
    for (var i = 0; i < expected.Count; i++) {
      Assert.AreEqual(expected[i].DirectoryFraction * 100, histogram[i].DirectoryPercent, 3.0, $"Directories of bucket {histogram[i].Bucket}");
    }
    // About 4 files per directory on average.
    var directories = histogram.Sum(b => b.DirectoryCount);
    var files = histogram.Sum(b => b.FileCount);
    Assert.AreEqual(4.4, (double)files / directories, 0.5, "Files per directory");
    // All the source files are in the histogram (the build output directory excluded).
    Assert.AreEqual(CorpusSpec.Medium.FileCount - CorpusSpec.Medium.FileCount / 50, files);
  }

  [TestMethod]
  public void PlanIsDeterministic() {
    var a = new CodeBaseGenerator(CorpusSpec.Small).ComputeFilesPerDirectoryHistogram();
    var b = new CodeBaseGenerator(CorpusSpec.Small).ComputeFilesPerDirectoryHistogram();
    CollectionAssert.AreEqual(a, b);
  }

  [TestMethod]
  public void SmallCorpusHasTheVeryLargeFilesAndTheMarkers() {
    var root = Path.Combine(Path.GetTempPath(), "VsChromiumBenchTests-" + Guid.NewGuid().ToString("N"));
    try {
      var manifest = new CodeBaseGenerator(CorpusSpec.Small).Generate(root);
      var large = manifest.Markers.Single(m => m.Text == CodeBaseGenerator.LargeFileMarker);
      Assert.IsGreaterThanOrEqualTo(CodeBaseGenerator.VeryLargeFileCount, large.FileCount);
      var veryLarge = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count(f => new FileInfo(f).Length >= 2 << 20);
      Assert.AreEqual(CodeBaseGenerator.VeryLargeFileCount, veryLarge);
      Assert.AreEqual(CorpusSpec.Small.FileCount, manifest.FileCount);
      Assert.AreEqual("none", manifest.Symlinks);
      Assert.IsNotNull(manifest.FilesPerDirectoryHistogram);
    } finally {
      Directory.Delete(root, recursive: true);
    }
  }

  [TestMethod]
  public void SymlinksAreCreatedOrSkippedWithAReason() {
    var root = Path.Combine(Path.GetTempPath(), "VsChromiumBenchTests-" + Guid.NewGuid().ToString("N"));
    try {
      var spec = CorpusSpec.Small with { Symlinks = true };
      var manifest = new CodeBaseGenerator(spec).Generate(root);
      TestContext.WriteLine($"Symlinks: {manifest.Symlinks}, {manifest.SymlinkFileCount} file links, {manifest.SymlinkDirectoryCount} directory links");
      if (manifest.Symlinks == "created") {
        Assert.AreEqual(10, manifest.SymlinkFileCount);
        Assert.AreEqual(5, manifest.SymlinkDirectoryCount);
        var links = WalkWithoutFollowingLinks(root).Select(p => new FileInfo(p)).Where(f => f.LinkTarget != null).ToList();
        Assert.HasCount(15, links);
        Assert.IsTrue(links.All(l => !Path.IsPathRooted(l.LinkTarget!) && Path.Exists(Path.Combine(l.DirectoryName!, l.LinkTarget!))));
      } else {
        StringAssert.StartsWith(manifest.Symlinks, "skipped (");
        Assert.AreEqual(0, manifest.SymlinkFileCount);
      }
      // The rest of the corpus is the same as without links.
      var without = new CodeBaseGenerator(CorpusSpec.Small).ComputeFilesPerDirectoryHistogram();
      CollectionAssert.AreEqual(without, manifest.FilesPerDirectoryHistogram);
    } finally {
      Directory.Delete(root, recursive: true);
    }
  }

  private static IEnumerable<string> WalkWithoutFollowingLinks(string directory) {
    foreach (var entry in Directory.EnumerateFileSystemEntries(directory)) {
      yield return entry;
      var info = new DirectoryInfo(entry);
      if (info.Exists && info.LinkTarget == null) {
        foreach (var child in WalkWithoutFollowingLinks(entry)) {
          yield return child;
        }
      }
    }
  }
}
