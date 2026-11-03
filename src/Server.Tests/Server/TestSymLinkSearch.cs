// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Server.Search;

namespace VsChromium.Tests.Server {
  /// <summary>
  /// Searches exclude the files of directories that are symbolic links (or junctions), or are
  /// contained in one, unless <see cref="SearchParams.IncludeSymLinks"/> is set.
  /// </summary>
  [TestClass]
  public class TestSymLinkSearch : ServerServicesTestBase {
    private const string Marker = "SYMLINK_SEARCH_MARKER";

    [TestMethod]
    public void FilesInJunctionsAreOnlyFoundWithIncludeSymLinks() {
      if (!OperatingSystem.IsWindows()) {
        Assert.Inconclusive("Directory junctions are a Windows feature");
      }
      var root = Path.Combine(Path.GetTempPath(), "VsChromiumSymLinkTest-" + Guid.NewGuid().ToString("N"));
      try {
        Directory.CreateDirectory(Path.Combine(root, "real", "sub"));
        File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"), "[SearchableFiles.include]\n*\n");
        File.WriteAllText(Path.Combine(root, "real", "sub", "file.txt"), Marker + "\n");
        if (!CreateJunction(Path.Combine(root, "link"), Path.Combine(root, "real"))) {
          Assert.Inconclusive("Unable to create a directory junction");
        }

        using (var container = CreateServerServices()) {
          var loaded = new ManualResetEventSlim(false);
          container.SearchEngine.FilesLoaded += (sender, args) => loaded.Set();
          container.FileRegistrationTracker.RegisterFileAsync(new FullPath(Path.Combine(root, "vs-chromium-project.txt")));
          Assert.IsTrue(loaded.Wait(TimeSpan.FromSeconds(30)), "Files not loaded");

          // real\sub\file.txt and link\sub\file.txt (the junction and its sub-directories).
          Assert.AreEqual(2, Search(container, includeSymLinks: true));
          Assert.AreEqual(1, Search(container, includeSymLinks: false));
        }
      }
      finally {
        DeleteDirectory(root);
      }
    }

    /// <summary>
    /// Deletes the test directory, retrying for a few seconds: the directory watcher of the server
    /// may release its handle after the server components are disposed.
    /// </summary>
    private static void DeleteDirectory(string root) {
      for (var attempt = 0; attempt < 20; attempt++) {
        try {
          // Note: Delete the junction itself first (this doesn't delete the files of its target).
          var link = Path.Combine(root, "link");
          if (Directory.Exists(link)) {
            Directory.Delete(link);
          }
          if (Directory.Exists(root)) {
            Directory.Delete(root, recursive: true);
          }
          return;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
          Thread.Sleep(250);
        }
      }
    }

    private static long Search(VsChromium.Server.ServerComponents container, bool includeSymLinks) {
      return container.SearchEngine.SearchCode(new SearchParams {
        SearchString = Marker,
        MaxResults = 100,
        MatchCase = true,
        IncludeSymLinks = includeSymLinks,
      }).HitCount;
    }

    private static bool CreateJunction(string link, string target) {
      using (var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
      })) {
        process.WaitForExit();
        return process.ExitCode == 0 && Directory.Exists(link);
      }
    }
  }
}
