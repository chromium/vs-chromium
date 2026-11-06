// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Globalization;
using System.Text;

namespace VsChromium.Bench.Cli;

/// <summary>
/// The non-interactive command line repeating a run exactly, and the script (.cmd on Windows, .sh
/// elsewhere) containing it.
/// </summary>
public static class RerunScript {
  /// <summary>
  /// The arguments of a non-interactive run with the resolved <paramref name="options"/> (all the
  /// choices made, interactively or not). The elevated questions are pre-answered with "no" unless
  /// they were answered "yes".
  /// </summary>
  public static List<string> ToArguments(BenchOptions options) {
    var args = new List<string> { "run", "--non-interactive" };
    void Add(string name, string? value) {
      if (value != null) {
        args.Add("--" + name);
        args.Add(value);
      }
    }
    Add("disk", options.Disk);
    Add("corpora-dir", options.CorporaDirectory);
    Add("corpus", options.Corpus);
    if (options.Symlinks == false) {
      args.Add("--no-symlinks");
    }
    Add("server", options.Server);
    if (options.Scenarios != null) {
      Add("scenarios", string.Join(",", options.Scenarios.Select(CommandLineParser.ScenarioName)));
    }
    Add("repeat", options.Repeat?.ToString(CultureInfo.InvariantCulture));
    Add("cache", options.Cache?.ToString().ToLowerInvariant());
    if (options.FlushCache != YesNoAsk.Ask) {
      Add("flush-cache", options.FlushCache == YesNoAsk.Yes ? "yes" : "no");
    }
    if (options.ListFilters != YesNoAsk.Ask) {
      Add("list-filters", options.ListFilters == YesNoAsk.Yes ? "yes" : "no");
    }
    if (!options.KeepServer) {
      args.Add("--no-keep-server");
    }
    if (options.ChangeCounts != null) {
      Add("changes", string.Join(",", options.ChangeCounts.Select(c => c.ToString(CultureInfo.InvariantCulture))));
    }
    if (options.ThreadCounts != null && options.Scenarios?.Contains(Scenario.Threads) == true) {
      Add("threads", string.Join(",", options.ThreadCounts.Select(c => c.ToString(CultureInfo.InvariantCulture))));
    }
    Add("results-dir", options.ResultsDirectory);
    Add("label", options.Label);
    Add("data-dir", options.DataDirectory);
    Add("report-dir", options.ReportDirectory);
    Add("machine-label", options.MachineLabel);
    if (options.RedactSecurity) {
      args.Add("--redact-security");
    }
    if (options.IncludeVirtualDisplays) {
      args.Add("--include-virtual-displays");
    }
    // The rerun of a rerun script doesn't write another script.
    Add("rerun-script", "none");
    return args;
  }

  /// <summary>The text of the script for Windows (cmd).</summary>
  public static string ToCmd(string benchPath, IReadOnlyList<string> args, string comment) {
    var text = new StringBuilder();
    text.Append("@echo off\r\n");
    foreach (var line in comment.Split('\n')) {
      text.Append("rem ").Append(line.TrimEnd('\r')).Append("\r\n");
    }
    text.Append(QuoteCmd(benchPath));
    foreach (var arg in args) {
      text.Append(' ').Append(QuoteCmd(arg));
    }
    text.Append(" %*\r\n");
    return text.ToString();
  }

  /// <summary>The text of the script for Linux and macOS (sh).</summary>
  public static string ToSh(string benchPath, IReadOnlyList<string> args, string comment) {
    var text = new StringBuilder();
    text.Append("#!/bin/sh\n");
    foreach (var line in comment.Split('\n')) {
      text.Append("# ").Append(line.TrimEnd('\r')).Append('\n');
    }
    text.Append("exec ").Append(QuoteSh(benchPath));
    foreach (var arg in args) {
      text.Append(' ').Append(QuoteSh(arg));
    }
    text.Append(" \"$@\"\n");
    return text.ToString();
  }

  /// <summary>Quotes an argument for cmd.exe (and the C runtime parsing of the command line).</summary>
  public static string QuoteCmd(string arg) {
    // "%" is expanded by cmd even inside quotes: doubled in a batch file.
    var escaped = arg.Replace("%", "%%");
    if (escaped.Length > 0 && escaped.All(c => char.IsAsciiLetterOrDigit(c) || "-_.:\\/,=+@".Contains(c))) {
      return escaped;
    }
    // Inside quotes: backslashes before a quote are doubled, quotes are escaped with a backslash.
    var result = new StringBuilder("\"");
    var backslashes = 0;
    foreach (var c in escaped) {
      if (c == '\\') {
        backslashes++;
        continue;
      }
      if (c == '"') {
        result.Append('\\', backslashes * 2 + 1).Append('"');
      } else {
        result.Append('\\', backslashes).Append(c);
      }
      backslashes = 0;
    }
    result.Append('\\', backslashes * 2).Append('"');
    return result.ToString();
  }

  /// <summary>Quotes an argument for the POSIX shell.</summary>
  public static string QuoteSh(string arg) {
    if (arg.Length > 0 && arg.All(c => char.IsAsciiLetterOrDigit(c) || "-_.:/,=+@".Contains(c))) {
      return arg;
    }
    return "'" + arg.Replace("'", "'\\''") + "'";
  }

  /// <summary>The default file name of the script of a run named <paramref name="name"/>.</summary>
  public static string DefaultFileName(string name, bool windows) {
    var safe = new string(name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray());
    return $"rerun-{safe}{(windows ? ".cmd" : ".sh")}";
  }

  /// <summary>Writes the script (executable on Linux and macOS) and returns its path.</summary>
  public static string Write(string path, string benchPath, IReadOnlyList<string> args, string comment) {
    var windows = OperatingSystem.IsWindows();
    var text = windows ? ToCmd(benchPath, args, comment) : ToSh(benchPath, args, comment);
    var directory = Path.GetDirectoryName(Path.GetFullPath(path));
    if (!string.IsNullOrEmpty(directory)) {
      Directory.CreateDirectory(directory);
    }
    File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    if (!OperatingSystem.IsWindows()) {
      File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
    return path;
  }
}
