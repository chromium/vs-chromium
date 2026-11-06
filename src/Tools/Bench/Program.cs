// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Bench;
using VsChromium.Bench.Cli;
using VsChromium.Bench.Servers;

BenchOptions options;
try {
  options = CommandLineParser.Parse(args);
} catch (CommandLineException e) {
  Console.Error.WriteLine($"Error: {e.Message}");
  Console.Error.WriteLine();
  Console.Error.WriteLine(CommandLineParser.Usage);
  return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
  // First Ctrl+C: stop after restoring the modified files of the corpus; second one: exit now.
  if (!cancellation.IsCancellationRequested) {
    e.Cancel = true;
    Console.Error.WriteLine("Stopping (the corpus is restored)... Press Ctrl+C again to exit now.");
    cancellation.Cancel();
  }
};

try {
  return await new BenchApp(options, Prompt.Console, Console.Out).RunAsync(cancellation.Token);
} catch (OperationCanceledException) {
  Console.Error.WriteLine("Cancelled.");
  return 130;
} catch (Exception e) when (e is CommandLineException or ServerNotFoundException) {
  Console.Error.WriteLine($"Error: {e.Message}");
  return 2;
}
