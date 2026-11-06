// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;

namespace VsChromium.Bench.Platform;

/// <summary>Result of an external command.</summary>
public sealed record CommandResult(int ExitCode, string Output, string Error) {
  public bool Success => ExitCode == 0;
}

/// <summary>Runs external commands (inventory tools: powershell, fltmc, diskutil, ...) with a timeout.</summary>
public static class ProcessRunner {
  /// <summary>Runs <paramref name="fileName"/>; null if it can't be started or times out.</summary>
  public static CommandResult? Run(string fileName, IEnumerable<string> arguments, TimeSpan? timeout = null) {
    var startInfo = new ProcessStartInfo {
      FileName = fileName,
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      RedirectStandardInput = true,
      StandardOutputEncoding = Encoding.UTF8,
      StandardErrorEncoding = Encoding.UTF8,
    };
    foreach (var argument in arguments) {
      startInfo.ArgumentList.Add(argument);
    }
    try {
      using var process = Process.Start(startInfo);
      if (process == null) {
        return null;
      }
      process.StandardInput.Close();
      var output = process.StandardOutput.ReadToEndAsync();
      var error = process.StandardError.ReadToEndAsync();
      if (!process.WaitForExit(timeout ?? TimeSpan.FromSeconds(30))) {
        try {
          process.Kill(entireProcessTree: true);
        } catch (InvalidOperationException) {
          // Already exited.
        }
        return null;
      }
      return new CommandResult(process.ExitCode, output.Result, error.Result);
    } catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or InvalidOperationException) {
      return null;
    }
  }

  /// <summary>Runs a PowerShell command (Windows), returns its output or null.</summary>
  public static string? PowerShell(string command, TimeSpan? timeout = null) {
    var result = Run("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], timeout);
    return result is { Success: true } ? result.Output : null;
  }
}
