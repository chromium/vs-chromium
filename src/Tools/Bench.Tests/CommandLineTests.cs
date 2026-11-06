// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Bench.Cli;

namespace VsChromium.Bench.Tests;

[TestClass]
public class CommandLineTests {
  [TestMethod]
  public void DefaultsToAnInteractiveRun() {
    var options = CommandLineParser.Parse([]);
    Assert.AreEqual(BenchCommand.Run, options.Command);
    Assert.IsFalse(options.NonInteractive);
    Assert.IsNull(options.Corpus);
    Assert.AreEqual(YesNoAsk.Ask, options.FlushCache);
  }

  [TestMethod]
  public void ParsesTheExampleOfTheDesign() {
    var options = CommandLineParser.Parse(
      "run --non-interactive --disk D: --corpus 1M --server release:v0.13.0 --scenarios cold,save,restart,live,offline --repeat 3 --cache warm".Split(' '));
    Assert.IsTrue(options.NonInteractive);
    Assert.AreEqual("D:", options.Disk);
    Assert.AreEqual("1M", options.Corpus);
    Assert.AreEqual("release:v0.13.0", options.Server);
    CollectionAssert.AreEqual(new[] { Scenario.Cold, Scenario.Save, Scenario.Restart, Scenario.Live, Scenario.Offline }, options.Scenarios!.ToArray());
    Assert.AreEqual(3, options.Repeat);
    Assert.AreEqual(CacheMode.Warm, options.Cache);
  }

  [TestMethod]
  public void ParsesInlineValuesListsAndAnswers() {
    var options = CommandLineParser.Parse(["--corpus=400K", "--changes", "1,1k,10_000", "--flush-cache", "yes", "--list-filters=no", "--scenarios", "7,1,1", "--no-keep-server", "--symlinks", "y"]);
    Assert.AreEqual("400k", options.Corpus);
    CollectionAssert.AreEqual(new[] { 1, 1000, 10000 }, options.ChangeCounts!.ToArray());
    Assert.AreEqual(YesNoAsk.Yes, options.FlushCache);
    Assert.AreEqual(YesNoAsk.No, options.ListFilters);
    // Sorted in the order of the design, without duplicates.
    CollectionAssert.AreEqual(new[] { Scenario.Cold, Scenario.RawFs }, options.Scenarios!.ToArray());
    Assert.IsFalse(options.KeepServer);
    Assert.IsTrue(options.Symlinks);
  }

  [TestMethod]
  public void ParsesTheOtherCommands() {
    var compare = CommandLineParser.Parse(["compare", "a.json", "b.json", "--output", "c.md"]);
    Assert.AreEqual(BenchCommand.Compare, compare.Command);
    CollectionAssert.AreEqual(new[] { "a.json", "b.json" }, compare.Files.ToArray());
    Assert.AreEqual("c.md", compare.Output);

    var cleanup = CommandLineParser.Parse(["cleanup", "--corpora", "--servers", "--yes"]);
    Assert.AreEqual(BenchCommand.Cleanup, cleanup.Command);
    CollectionAssert.AreEqual(new[] { "corpora", "servers" }, cleanup.CleanupTargets!.ToArray());
    Assert.IsTrue(cleanup.Yes);

    Assert.AreEqual(BenchCommand.Inventory, CommandLineParser.Parse(["inventory", "--json"]).Command);
    Assert.AreEqual(BenchCommand.Help, CommandLineParser.Parse(["--help"]).Command);
  }

  [TestMethod]
  public void RejectsInvalidCommandLines() {
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["bogus"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["--corpus", "3M"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["--repeat", "0"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["--repeat"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["--scenarios", "cold,warp"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["--flush-cache", "maybe"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["--non-interactive=yes"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["--server", "release:"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["compare"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["run", "--corpora"]));
    Assert.ThrowsExactly<CommandLineException>(() => CommandLineParser.Parse(["--unknown"]));
  }

  [TestMethod]
  public void RerunArgumentsRoundTrip() {
    var resolved = new BenchOptions {
      NonInteractive = false,
      Disk = "D:",
      Corpus = "1M",
      Symlinks = true,
      Server = "release:v0.13.0",
      Scenarios = [Scenario.Cold, Scenario.Live, Scenario.RawFs],
      Repeat = 2,
      Cache = CacheMode.Cold,
      FlushCache = YesNoAsk.Yes,
      ListFilters = YesNoAsk.No,
      KeepServer = false,
      ChangeCounts = [1, 1000],
      Label = "my machine, D: dev drive",
      RerunScript = "x.cmd",
    };
    var args = RerunScript.ToArguments(resolved);
    CollectionAssert.Contains(args, "--non-interactive");
    var parsed = CommandLineParser.Parse(args);
    Assert.IsTrue(parsed.NonInteractive);
    Assert.AreEqual("none", parsed.RerunScript);
    Assert.AreEqual(resolved with { NonInteractive = true, RerunScript = "none", Files = parsed.Files }, parsed with { Scenarios = resolved.Scenarios, ChangeCounts = resolved.ChangeCounts });
    CollectionAssert.AreEqual(resolved.Scenarios.ToArray(), parsed.Scenarios!.ToArray());
    CollectionAssert.AreEqual(resolved.ChangeCounts.ToArray(), parsed.ChangeCounts!.ToArray());
  }

  [TestMethod]
  public void CmdScriptQuotesArguments() {
    var script = RerunScript.ToCmd(@"C:\Program Files\bench\vschromium-bench.exe", ["run", "--label", "50% \"fast\" disk", "--disk", "D:"], "line 1\nline 2");
    StringAssert.StartsWith(script, "@echo off\r\nrem line 1\r\nrem line 2\r\n");
    StringAssert.Contains(script, "\"C:\\Program Files\\bench\\vschromium-bench.exe\" run --label \"50%% \\\"fast\\\" disk\" --disk D: %*\r\n");
    Assert.AreEqual("plain", RerunScript.QuoteCmd("plain"));
    Assert.AreEqual("\"a b\\\\\"", RerunScript.QuoteCmd("a b\\"));
    Assert.AreEqual("\"\"", RerunScript.QuoteCmd(""));
  }

  [TestMethod]
  public void ShScriptQuotesArguments() {
    var script = RerunScript.ToSh("/opt/bench/vschromium-bench", ["run", "--label", "it's a test", "--disk", "/mnt/data"], "comment");
    Assert.AreEqual("#!/bin/sh\n# comment\nexec /opt/bench/vschromium-bench run --label 'it'\\''s a test' --disk /mnt/data \"$@\"\n", script);
  }

  [TestMethod]
  public void WritesAnExecutableScript() {
    var path = Path.Combine(Path.GetTempPath(), $"rerun-test-{Guid.NewGuid():N}" + (OperatingSystem.IsWindows() ? ".cmd" : ".sh"));
    try {
      RerunScript.Write(path, "bench", ["run"], "test");
      Assert.IsTrue(File.Exists(path));
      if (!OperatingSystem.IsWindows()) {
        Assert.IsTrue(File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute));
      }
    } finally {
      File.Delete(path);
    }
    Assert.AreEqual("rerun-host-D-400k.cmd", RerunScript.DefaultFileName("host-D-400k", windows: true));
    Assert.AreEqual("rerun-a-b.sh", RerunScript.DefaultFileName("a b", windows: false));
  }
}
