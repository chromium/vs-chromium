// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Globalization;
using VsChromium.Server.Cache;

namespace VsChromium.Server {
  /// <summary>
  /// The command line options of the server: the index cache (<see cref="IndexCacheOptions"/>) and the
  /// benchmark options (used by vschromium-bench, src/Tools/Bench; not for normal use).
  /// </summary>
  public sealed class ServerOptions {
    /// <summary>Enables the "bench/*" requests (listed by "server/initialize" in "bench.requests").</summary>
    public const string BenchArgument = "--bench";

    /// <summary>
    /// The number of threads of the indexing worker pool (0: one per processor), all of them always
    /// running (no throttling when the machine is busy): measurements without noise, thread scaling.
    /// </summary>
    public const string ThreadsArgument = "--threads";

    /// <summary>
    /// The delay without new change after which the file system changes are processed (debouncing),
    /// in milliseconds (default: 500 ms for file changes, 1 s for creations and deletions).
    /// </summary>
    public const string WatcherDelayArgument = "--watcher-delay-ms";

    /// <summary>
    /// The tree of the index cache is verified by a full scan, not from the change journal (the
    /// checkpoints of the journal are still saved): compares the two on the same machine.
    /// </summary>
    public const string NoJournalArgument = "--no-journal";

    public ServerOptions() {
      IndexCache = IndexCacheOptions.Disabled;
    }

    public IndexCacheOptions IndexCache { get; set; }
    public bool Bench { get; set; }
    /// <summary>Null: the default worker pool; 0: one thread per processor.</summary>
    public int? Threads { get; set; }
    public TimeSpan? WatcherDelay { get; set; }
    public bool NoJournal { get; set; }

    /// <summary>The options of the command line. Throws <see cref="ArgumentException"/> for invalid values.</summary>
    public static ServerOptions FromCommandLine(string[] args) {
      var options = new ServerOptions { IndexCache = IndexCacheOptions.FromCommandLine(args) };
      for (var i = 0; i < args.Length; i++) {
        switch (args[i]) {
          case BenchArgument:
            options.Bench = true;
            break;
          case NoJournalArgument:
            options.NoJournal = true;
            break;
          case ThreadsArgument:
            options.Threads = ParseInt(args, ref i, ThreadsArgument, min: 0);
            break;
          case WatcherDelayArgument:
            options.WatcherDelay = TimeSpan.FromMilliseconds(ParseInt(args, ref i, WatcherDelayArgument, min: 0));
            break;
        }
      }
      return options;
    }

    private static int ParseInt(string[] args, ref int index, string name, int min) {
      if (index + 1 >= args.Length ||
          !int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min) {
        throw new ArgumentException($"{name}: expected a number >= {min}.");
      }
      index++;
      return value;
    }
  }
}
