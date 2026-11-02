// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// Correctness of incremental index updates after many changes at once, as done by a "git checkout"
/// in a large code base: thousands of files modified, created, deleted, moved, directories created
/// and deleted. The index built incrementally must be the same as an index built from scratch on
/// the resulting tree. See "Incremental updates" in docs/portable-server/PORTING_PRINCIPLES.md.
/// </summary>
[TestClass]
public class IncrementalUpdateTests {
  public TestContext TestContext { get; set; } = null!;

  private const int NoLimit = 10_000_000;

  /// <summary>
  /// The generated tree and the changes of the "checkout", for a given size.
  /// </summary>
  private sealed class Checkout(int directoryCount, int filesPerDirectory) {
    /// <summary>
    /// Category of each generated file, from its index: what the "checkout" does to it.
    /// </summary>
    private char Category(int directory, int file) {
      if (directory == directoryCount - 1) return 'X'; // directory deleted
      return (file % 10) switch {
        0 or 1 or 2 or 3 => 'M', // modified
        4 => 'D', // deleted
        5 => 'R', // moved to another directory
        6 => 'T', // touched (rewritten with the same contents)
        _ => 'K', // kept
      };
    }

    private static string FileContents(string marker, int directory, int file) {
      var sb = new StringBuilder();
      sb.Append("// Generated file ").Append(directory).Append('/').Append(file).Append('\n');
      sb.Append("// ").Append(marker).Append('\n');
      for (var line = 0; line < 20; line++) {
        sb.Append("void Function").Append(line).Append("() { return INCREMENTAL_COMMON_TEXT; }\n");
      }
      return sb.ToString();
    }

    private static string FilePath(string root, int directory, int file) =>
      Path.Combine(root, $"dir{directory:D3}", $"file{file:D3}.cc");

    public void GenerateTree(string root) {
      File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"),
        "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
      Parallel.For(0, directoryCount, d => {
        Directory.CreateDirectory(Path.Combine(root, $"dir{d:D3}"));
        for (var f = 0; f < filesPerDirectory; f++) {
          var marker = $"ORIG_{Category(d, f)}_{d:D3}_{f:D3}";
          File.WriteAllText(FilePath(root, d, f), FileContents(marker, d, f));
        }
      });
    }

    /// <summary>
    /// Applies the changes of a "checkout", as fast as possible (as git does, on several threads).
    /// </summary>
    public void Apply(string root) {
      for (var m = 0; m < 5; m++) {
        Directory.CreateDirectory(Path.Combine(root, $"moved{m}"));
      }
      Parallel.For(0, directoryCount - 1, d => {
        for (var f = 0; f < filesPerDirectory; f++) {
          var path = FilePath(root, d, f);
          switch (Category(d, f)) {
            case 'M':
              File.WriteAllText(path, FileContents($"CHANGED_M_{d:D3}_{f:D3}", d, f));
              break;
            case 'D':
              File.Delete(path);
              break;
            case 'R':
              File.Move(path, Path.Combine(root, $"moved{d % 5}", $"moved_{d:D3}_{f:D3}.cc"));
              break;
            case 'T':
              File.WriteAllText(path, File.ReadAllText(path));
              break;
          }
        }
      });
      Directory.Delete(Path.Combine(root, $"dir{directoryCount - 1:D3}"), recursive: true);
      Parallel.For(0, 10, n => {
        var directory = Path.Combine(root, $"new{n:D2}", "sub");
        Directory.CreateDirectory(directory);
        for (var f = 0; f < 50; f++) {
          File.WriteAllText(Path.Combine(directory, $"new_{f:D3}.cc"), FileContents($"NEW_{n:D2}_{f:D3}", 1000 + n, f));
        }
      });
    }

    private int PerCategory(int count) => count * (filesPerDirectory / 10) * (directoryCount - 1);

    public long ModifiedCount => PerCategory(4);

    /// <summary>Expected number of hits of each marker prefix after the checkout.</summary>
    public (string Prefix, long Hits)[] ExpectedAfterCheckout => new (string, long)[] {
      ("ORIG_M_", 0),
      ("CHANGED_M_", PerCategory(4)),
      ("ORIG_D_", 0),
      ("ORIG_R_", PerCategory(1)),
      ("ORIG_T_", PerCategory(1)),
      ("ORIG_K_", PerCategory(3)),
      ("ORIG_X_", 0),
      ("NEW_", 10 * 50),
    };
  }

  private static readonly string[] ComparisonQueries = {
    "ORIG_", "CHANGED_M_", "NEW_", "Generated file",
  };

  [TestMethod]
  [Timeout(15 * 60 * 1000, CooperativeCancellation = true)]
  [DataRow(40, 100, 0, DisplayName = "4,000 files, ~2,500 changes")]
  [DataRow(100, 300, 0, DisplayName = "30,000 files, ~20,000 changes")]
  // Small OS notification buffers (4 KB): notifications overflow, the server must rescan.
  [DataRow(100, 300, 4096, DisplayName = "30,000 files, ~20,000 changes, notification overflows")]
  public async Task CheckoutLikeChanges_IncrementalIndexIsSameAsFreshIndex(int directoryCount, int filesPerDirectory, int watcherBufferSize) {
    var checkout = new Checkout(directoryCount, filesPerDirectory);
    var root = Path.Combine(Path.GetTempPath(), "VsChromiumIncrementalTest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
      checkout.GenerateTree(root);
      var projectFile = Path.Combine(root, "vs-chromium-project.txt");

      Environment.SetEnvironmentVariable("VSCHROMIUM_WATCHER_BUFFER_SIZE", watcherBufferSize > 0 ? watcherBufferSize.ToString() : null);
      StdioServerClient serverInstance;
      try {
        serverInstance = await StdioServerClient.StartAsync(TestSettings.ServerPath);
      }
      finally {
        Environment.SetEnvironmentVariable("VSCHROMIUM_WATCHER_BUFFER_SIZE", null);
      }
      await using var server = serverInstance;
      await server.WaitForEventAsync("SearchEngineFilesLoaded", TimeSpan.FromMinutes(2),
        () => server.RequestAsync(ServerProtocol.RegisterFile(projectFile)));
      Assert.AreEqual(checkout.ModifiedCount, await HitCountAsync(server, "ORIG_M_"));

      // "Checkout", then wait until the index reflects all the changes.
      var stopwatch = Stopwatch.StartNew();
      checkout.Apply(root);
      var applied = stopwatch.Elapsed;
      var consistent = false;
      while (stopwatch.Elapsed < TimeSpan.FromMinutes(5)) {
        consistent = true;
        foreach (var (prefix, hits) in checkout.ExpectedAfterCheckout) {
          if (await HitCountAsync(server, prefix) != hits) {
            consistent = false;
            break;
          }
        }
        if (consistent)
          break;
        await Task.Delay(100);
      }
      var message = $"Checkout applied in {applied.TotalMilliseconds:n0} ms, index consistent after {stopwatch.Elapsed.TotalMilliseconds:n0} ms";
      TestContext.WriteLine(message);
      Console.WriteLine(message);
      foreach (var (prefix, hits) in checkout.ExpectedAfterCheckout) {
        Assert.AreEqual(hits, await HitCountAsync(server, prefix), $"Hits of \"{prefix}\" after the checkout");
      }
      Assert.IsTrue(consistent);

      // Let pending notifications settle, then compare with an index built from scratch.
      await Task.Delay(TimeSpan.FromSeconds(3));
      await using var freshServer = await StdioServerClient.StartAsync(TestSettings.ServerPath);
      await freshServer.WaitForEventAsync("SearchEngineFilesLoaded", TimeSpan.FromMinutes(2),
        () => freshServer.RequestAsync(ServerProtocol.RegisterFile(projectFile)));
      foreach (var query in ComparisonQueries) {
        var incremental = await ResultsAsync(server, query);
        var fresh = await ResultsAsync(freshServer, query);
        CollectionAssert.AreEqual(fresh, incremental, $"Results of \"{query}\" (incremental vs fresh index)");
      }
      var incrementalPaths = await PathResultsAsync(server, "*.cc");
      var freshPaths = await PathResultsAsync(freshServer, "*.cc");
      Assert.IsNotEmpty(freshPaths);
      CollectionAssert.AreEqual(freshPaths, incrementalPaths, "File paths (incremental vs fresh index)");
    }
    finally {
      try {
        Directory.Delete(root, recursive: true);
      }
      catch (IOException) {
        // Best effort (e.g. a file still open by a server being stopped)
      }
    }
  }

  private static async Task<long> HitCountAsync(StdioServerClient server, string text) {
    var response = await server.RequestAsync(ServerProtocol.SearchCode(text, maxResults: NoLimit, matchCase: true));
    return response.Data["hitCount"]!.GetValue<long>();
  }

  /// <summary>All the matches of <paramref name="text"/>, as sorted "path:position:length" strings.</summary>
  private static async Task<List<string>> ResultsAsync(StdioServerClient server, string text) {
    var response = await server.RequestAsync(ServerProtocol.SearchCode(text, maxResults: NoLimit, matchCase: true));
    return ServerProtocol.FlattenResults(response.Data["searchResults"]?.AsObject())
      .SelectMany(file => file.Positions.Select(p => $"{file.FullPath}:{p.Position}:{p.Length}"))
      .OrderBy(x => x, StringComparer.Ordinal)
      .ToList();
  }

  private static async Task<List<string>> PathResultsAsync(StdioServerClient server, string pattern) {
    var response = await server.RequestAsync(ServerProtocol.SearchFilePaths(pattern, maxResults: NoLimit));
    return ServerProtocol.FlattenResults(response.Data["searchResult"]?.AsObject())
      .Select(file => file.FullPath)
      .OrderBy(x => x, StringComparer.Ordinal)
      .ToList();
  }
}
