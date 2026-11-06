// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Bench.Cli;

/// <summary>Interactive questions on the console, with defaults (Enter accepts the default).</summary>
public sealed class Prompt(TextReader input, TextWriter output) {
  public static Prompt Console { get; } = new(System.Console.In, System.Console.Out);

  /// <summary>A menu: returns the index of the chosen item. Disabled items (with a reason) can't be chosen.</summary>
  public int Choose(string title, IReadOnlyList<(string Text, string? DisabledReason)> items, int defaultIndex) {
    output.WriteLine();
    output.WriteLine(title);
    for (var i = 0; i < items.Count; i++) {
      var (text, disabled) = items[i];
      output.WriteLine($"  {(i == defaultIndex ? "*" : " ")}{i + 1}. {text}{(disabled != null ? $"  [not available: {disabled}]" : "")}");
    }
    while (true) {
      output.Write($"Choice [{defaultIndex + 1}]: ");
      var line = input.ReadLine();
      if (line == null) {
        throw new OperationCanceledException("End of input.");
      }
      line = line.Trim();
      if (line.Length == 0) {
        return defaultIndex;
      }
      if (int.TryParse(line, out var choice) && choice >= 1 && choice <= items.Count) {
        if (items[choice - 1].DisabledReason is { } reason) {
          output.WriteLine($"Not available: {reason}");
          continue;
        }
        return choice - 1;
      }
      output.WriteLine($"Enter a number from 1 to {items.Count}.");
    }
  }

  /// <summary>A free text answer (the default if empty).</summary>
  public string Ask(string question, string defaultValue) {
    output.Write($"{question} [{defaultValue}]: ");
    var line = input.ReadLine() ?? throw new OperationCanceledException("End of input.");
    return line.Trim().Length == 0 ? defaultValue : line.Trim();
  }

  public bool YesNo(string question, bool defaultValue) {
    while (true) {
      output.Write($"{question} [{(defaultValue ? "Y/n" : "y/N")}]: ");
      var line = (input.ReadLine() ?? throw new OperationCanceledException("End of input.")).Trim().ToLowerInvariant();
      if (line.Length == 0) {
        return defaultValue;
      }
      if (line is "y" or "yes") {
        return true;
      }
      if (line is "n" or "no") {
        return false;
      }
    }
  }
}
