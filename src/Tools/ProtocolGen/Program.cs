// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.ProtocolGen.Emitters;
using VsChromium.ProtocolGen.Model;
using VsChromium.ProtocolGen.Syntax;

namespace VsChromium.ProtocolGen;

/// <summary>
/// Command line: generates the protocol code from the definition files of a directory.
/// <code>
/// VsChromium.ProtocolGen --input &lt;dir&gt; [--server-cs &lt;file&gt;] [--ts &lt;file&gt;] [--vs-cs &lt;file&gt;] [--check]
/// </code>
/// Outputs: "--server-cs": server (C#), "--ts": VS Code extension (TypeScript), "--vs-cs": Visual
/// Studio extension (C# 7.3).
/// Files are only written when their contents change (so that incremental builds stay incremental).
/// With "--check", nothing is written: the exit code is 1 if a file is not up to date.
/// </summary>
public static class Program {
  public static int Main(string[] args) {
    string? input = null;
    string? serverCs = null;
    string? ts = null;
    string? vsCs = null;
    var check = false;
    for (var i = 0; i < args.Length; i++) {
      switch (args[i]) {
        case "--input" when i + 1 < args.Length:
          input = args[++i];
          break;
        case "--server-cs" when i + 1 < args.Length:
          serverCs = args[++i];
          break;
        case "--ts" when i + 1 < args.Length:
          ts = args[++i];
          break;
        case "--vs-cs" when i + 1 < args.Length:
          vsCs = args[++i];
          break;
        case "--check":
          check = true;
          break;
        default:
          return Usage($"Unknown argument \"{args[i]}\"");
      }
    }
    if (input == null)
      return Usage("Missing --input");

    ProtocolDefinition protocol;
    try {
      protocol = ProtocolBinder.LoadDirectory(input);
    }
    catch (ProtocolDefinitionException e) {
      // MSBuild/Visual Studio error format: "file(line,col): error: message"
      Console.Error.WriteLine(Path.Combine(input, e.Message));
      return 1;
    }

    var outputs = new List<(string Path, string Contents)>();
    if (serverCs != null)
      outputs.Add((serverCs, CSharpServerEmitter.Emit(protocol)));
    if (ts != null)
      outputs.Add((ts, TypeScriptEmitter.Emit(protocol)));
    if (vsCs != null)
      outputs.Add((vsCs, CSharpClientEmitter.Emit(protocol)));

    var result = 0;
    foreach (var (path, contents) in outputs) {
      // Note: Line endings are ignored (the repository stores text files with LF line endings, the
      // working copy may use CRLF): generated files are written with the platform line endings.
      var current = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
      if (current == contents) {
        continue;
      }
      if (check) {
        Console.Error.WriteLine($"{path}: error: The generated file is not up to date (run the build, or VsChromium.ProtocolGen)");
        result = 1;
        continue;
      }
      Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
      File.WriteAllText(path, contents.Replace("\n", Environment.NewLine));
      Console.WriteLine($"Generated {path}");
    }
    return result;
  }

  private static int Usage(string error) {
    Console.Error.WriteLine("error: " + error);
    Console.Error.WriteLine("Usage: VsChromium.ProtocolGen --input <protocol directory> [--server-cs <file>] [--ts <file>] [--vs-cs <file>] [--check]");
    Console.Error.WriteLine("  --server-cs <file>  Server code (C#, System.Text.Json)");
    Console.Error.WriteLine("  --ts <file>         VS Code extension code (TypeScript, vscode-jsonrpc)");
    Console.Error.WriteLine("  --vs-cs <file>      Visual Studio extension code (C# 7.3, Newtonsoft.Json, StreamJsonRpc)");
    Console.Error.WriteLine("  --check             Write nothing, exit code 1 if a file is not up to date");
    return 2;
  }
}
