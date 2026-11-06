// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;

namespace VsChromium.Bench.Shared.Corpus;

/// <summary>
/// Generates a deterministic, real-world-like large code base (Chromium-like): realistic directory
/// structure, file names, extensions and size distribution, C++/Python/GN/Java/... looking contents,
/// a mix of ASCII, UTF-8 (with and without BOM), CRLF and binary files, and known strings
/// ("markers") planted at known places so that search results can be verified exactly.
///
/// The layout and contents only depend on <see cref="CorpusSpec"/> and <see cref="Version"/>:
/// increment <see cref="Version"/> whenever the generated output changes.
/// </summary>
public sealed class CodeBaseGenerator(CorpusSpec spec) {
  public const int Version = 1;

  // Markers planted in the code base (see CorpusManifest.Markers for the exact counts).
  public const string UniqueMarkerPrefix = "VSCB_UNIQUE_MARKER_";
  public const string RareMarker = "VSCB_RARE_MARKER";
  public const string CommonMarker = "VSCB_COMMON_MARKER";
  public const string LargeFileMarker = "VSCB_LARGE_FILE_MARKER";
  public const int UniqueMarkerCount = 10;

  private const int MaxDepth = 9;

  private enum Kind { Cpp, Header, ObjC, Python, Gn, Mojom, Java, JavaScript, TypeScript, Json, Html, Markdown, Text, Proto, Binary }

  private sealed record FilePlan(string RelativePath, Kind Kind, int Size, int Seed, bool Utf8Bom, bool Crlf, bool NonAscii) {
    public List<string> Markers { get; } = new();
  }

  private static readonly (Kind Kind, string Ext, int Weight)[] Extensions = [
    (Kind.Cpp, ".cc", 300), (Kind.Header, ".h", 0 /* generated with .cc */), (Kind.ObjC, ".mm", 15),
    (Kind.Python, ".py", 50), (Kind.Gn, ".gn", 30), (Kind.Gn, ".gni", 8), (Kind.Mojom, ".mojom", 15),
    (Kind.Java, ".java", 60), (Kind.JavaScript, ".js", 40), (Kind.TypeScript, ".ts", 30), (Kind.Json, ".json", 20),
    (Kind.Html, ".html", 20), (Kind.Markdown, ".md", 15), (Kind.Text, ".txt", 10), (Kind.Proto, ".proto", 8),
    (Kind.Binary, ".png", 25), (Kind.Binary, ".zip", 3), (Kind.Binary, ".pak", 2),
  ];

  private static readonly string[] TopLevelDirectories = [
    "base", "build", "cc", "chrome", "components", "content", "device", "gpu", "ipc", "media", "mojo", "net",
    "services", "skia", "sql", "storage", "testing", "third_party", "tools", "ui", "url", "v8", "docs",
  ];

  private static readonly string[] DirectoryWords = [
    "browser", "renderer", "common", "public", "test", "internal", "android", "win", "mac", "linux", "chromeos",
    "ios", "cpp", "mojom", "impl", "util", "metrics", "prefs", "policy", "sync", "network", "cache", "input",
    "layout", "paint", "animation", "accessibility", "extensions", "devtools", "download", "history", "bookmarks",
    "omnibox", "autofill", "password_manager", "permissions", "payments", "safe_browsing", "signin", "webui",
    "resources", "strings", "core", "platform", "bindings", "modules", "frame", "page", "loader", "fetch",
    "workers", "scheduler", "gfx", "geometry", "views", "controls", "compositor", "display", "events", "gl",
    "vulkan", "ozone", "wayland", "x11", "java", "src", "org", "chromium", "features", "service", "client",
  ];

  private static readonly string[] Words = [
    "web", "contents", "frame", "host", "render", "process", "browser", "context", "navigation", "request", "url",
    "loader", "network", "service", "client", "delegate", "observer", "manager", "controller", "handler", "view",
    "widget", "window", "tab", "profile", "settings", "pref", "policy", "sync", "storage", "cache", "file", "path",
    "stream", "buffer", "message", "pipe", "channel", "task", "runner", "thread", "sequence", "callback", "event",
    "input", "layout", "paint", "layer", "surface", "display", "compositor", "gpu", "texture", "shader", "image",
    "font", "text", "string", "token", "parser", "encoder", "decoder", "codec", "audio", "video", "media", "device",
    "permission", "extension", "download", "history", "bookmark", "search", "omnibox", "autofill", "password",
    "account", "identity", "metrics", "histogram", "trace", "debug", "test", "util", "helper", "factory", "builder",
    "provider", "registry", "store", "model", "state", "config", "feature", "flag", "switch", "origin", "site",
    "security", "certificate", "cookie", "session", "worker", "script", "module", "binding", "node", "element",
    "document", "style", "selector", "animation", "timer", "scheduler", "queue", "pool", "allocator", "memory",
  ];

  private static readonly string[] FileSuffixes = ["", "", "", "", "_impl", "_util", "_factory", "_observer", "_delegate", "_win", "_mac", "_android", "_linux"];

  private static readonly string[] NonAsciiLines = [
    "Copyright © The Chromium Authors — naïve café résumé",
    "Author: Jürgen Müller (Zürich) • Åsa Øberg",
    "日本語のテスト文字列: 検索エンジン",
    "中文测试: 搜索引擎索引",
    "Ελληνικά κείμενο για δοκιμή",
    "Русский текст для проверки поиска",
    "emoji test 🎉🚀 surrogate pairs",
    "العربية نص تجريبي",
  ];

  private readonly Random _random = new(spec.Seed);

  public CorpusManifest Generate(string rootPath) {
    var directories = PlanDirectories();
    var files = PlanFiles(directories);
    var markers = PlantMarkers(files);

    Directory.CreateDirectory(rootPath);
    foreach (var dir in directories) {
      Directory.CreateDirectory(Path.Combine(rootPath, dir));
    }
    File.WriteAllText(Path.Combine(rootPath, "vs-chromium-project.txt"), ProjectFileContents);

    long totalBytes = 0;
    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file => {
      var bytes = GenerateFile(file);
      File.WriteAllBytes(Path.Combine(rootPath, file.RelativePath), bytes);
      Interlocked.Add(ref totalBytes, bytes.Length);
    });

    return new CorpusManifest(
      GeneratorVersion: Version,
      Spec: spec,
      FileCount: files.Count,
      DirectoryCount: directories.Count,
      TotalBytes: totalBytes,
      SearchableFileCount: files.Count(f => f.Kind != Kind.Binary && !IsUnderOut(f.RelativePath)),
      CreatedUtc: DateTime.UtcNow,
      Markers: markers);
  }

  /// <summary>Project file: "out/" is ignored (as in Chromium), binary files are not searchable.</summary>
  private const string ProjectFileContents =
    "[SourceExplorer.ignore]\nout/\n.git/\n\n" +
    "[SearchableFiles.include]\n*\n\n" +
    "[SearchableFiles.ignore]\n*.png\n*.zip\n*.pak\n*.o\n*.obj\n";

  private static bool IsUnderOut(string relativePath) => relativePath.StartsWith("out" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

  // ---------------------------------------------------------------------------------------------
  // Planning (sequential, deterministic)

  private List<string> PlanDirectories() {
    var dirs = new List<(string Path, int Depth)>();
    var children = new Dictionary<string, HashSet<string>>();
    foreach (var top in TopLevelDirectories) {
      dirs.Add((top, 1));
    }
    dirs.Add(("out", 1));
    dirs.Add((Path.Combine("out", "Default"), 2));
    dirs.Add((Path.Combine("out", "Default", "obj"), 3));

    var target = Math.Max(30, spec.FileCount / 10);
    while (dirs.Count < target) {
      var parent = dirs[_random.Next(dirs.Count)];
      if (parent.Depth >= MaxDepth || IsUnderOut(parent.Path + Path.DirectorySeparatorChar)) {
        continue;
      }
      var name = DirectoryWords[_random.Next(DirectoryWords.Length)];
      if (!children.TryGetValue(parent.Path, out var siblings)) {
        children[parent.Path] = siblings = new HashSet<string>();
      }
      if (!siblings.Add(name)) {
        name = name + "_" + Words[_random.Next(Words.Length)];
        if (!siblings.Add(name)) {
          continue;
        }
      }
      dirs.Add((Path.Combine(parent.Path, name), parent.Depth + 1));
    }
    return dirs.Select(d => d.Path).ToList();
  }

  private List<FilePlan> PlanFiles(List<string> directories) {
    var files = new List<FilePlan>();
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var sourceDirs = directories.Where(d => !IsUnderOut(d + Path.DirectorySeparatorChar)).ToList();
    var outObjDir = Path.Combine("out", "Default", "obj");
    var totalWeight = Extensions.Sum(e => e.Weight);

    while (files.Count < spec.FileCount) {
      // ~2% of the files are build outputs (ignored by the project file).
      if (_random.NextDouble() < 0.02) {
        var objName = Path.Combine(outObjDir, $"{RandomIdentifier("_")}_{files.Count}.obj");
        files.Add(NewFile(objName, Kind.Binary));
        continue;
      }
      var dir = sourceDirs[_random.Next(sourceDirs.Count)];
      var (kind, ext) = PickExtension(totalWeight);
      var baseName = RandomIdentifier("_") + FileSuffixes[_random.Next(FileSuffixes.Length)];
      if (kind is Kind.Cpp && _random.NextDouble() < 0.15) {
        baseName += "_unittest";
      }
      if (kind is Kind.Gn) {
        baseName = ext == ".gn" ? "BUILD" : baseName;
      }
      var path = Path.Combine(dir, baseName + ext);
      if (!names.Add(path)) {
        continue;
      }
      files.Add(NewFile(path, kind));
      // Most .cc files come with a header.
      if (kind == Kind.Cpp && !baseName.EndsWith("_unittest", StringComparison.Ordinal) && _random.NextDouble() < 0.7) {
        var header = Path.Combine(dir, baseName + ".h");
        if (names.Add(header)) {
          files.Add(NewFile(header, Kind.Header));
        }
      }
    }
    return files;
  }

  private FilePlan NewFile(string path, Kind kind) {
    int size;
    if (kind != Kind.Binary && _random.NextDouble() < 0.001) {
      size = _random.Next(2 << 20, 16 << 20); // Rare very large (generated) files: 2-16 MB.
    } else {
      // Log-normal distribution: median ~5 KB, long tail.
      var normal = Math.Sqrt(-2 * Math.Log(1 - _random.NextDouble())) * Math.Cos(2 * Math.PI * _random.NextDouble());
      size = (int)Math.Clamp(Math.Exp(Math.Log(5000) + 1.1 * normal), 64, 1_500_000);
    }
    var text = kind != Kind.Binary;
    return new FilePlan(path, kind, size, _random.Next(),
      Utf8Bom: text && _random.NextDouble() < 0.005,
      Crlf: text && _random.NextDouble() < 0.05,
      NonAscii: text && _random.NextDouble() < 0.03);
  }

  private (Kind, string) PickExtension(int totalWeight) {
    var n = _random.Next(totalWeight);
    foreach (var (kind, ext, weight) in Extensions) {
      if (n < weight) {
        return (kind, ext);
      }
      n -= weight;
    }
    return (Kind.Cpp, ".cc");
  }

  private List<PlantedMarker> PlantMarkers(List<FilePlan> files) {
    // Markers go in searchable text files, outside of ignored directories, where comments are valid.
    var eligible = files.Where(f => f.Kind is Kind.Cpp or Kind.Header or Kind.Python or Kind.Java or Kind.Markdown or Kind.Text
                                    && !IsUnderOut(f.RelativePath)).ToList();
    var markers = new List<PlantedMarker>();

    for (var i = 0; i < UniqueMarkerCount; i++) {
      var text = UniqueMarkerPrefix + i.ToString("D2");
      eligible[_random.Next(eligible.Count)].Markers.Add(text);
      markers.Add(new PlantedMarker(text, 1, 1));
    }

    var rare = Math.Min(25, eligible.Count);
    foreach (var file in eligible.OrderBy(_ => _random.Next()).Take(rare)) {
      file.Markers.Add(RareMarker);
    }
    markers.Add(new PlantedMarker(RareMarker, rare, rare));

    int commonFiles = 0, commonHits = 0;
    foreach (var file in eligible) {
      if (_random.NextDouble() < 0.02) {
        var count = _random.Next(1, 4);
        for (var i = 0; i < count; i++) {
          file.Markers.Add(CommonMarker);
        }
        commonFiles++;
        commonHits += count;
      }
    }
    markers.Add(new PlantedMarker(CommonMarker, commonFiles, commonHits));

    var largeFiles = eligible.Where(f => f.Size >= 1 << 20).ToList();
    foreach (var file in largeFiles) {
      file.Markers.Add(LargeFileMarker);
    }
    markers.Add(new PlantedMarker(LargeFileMarker, largeFiles.Count, largeFiles.Count));
    return markers;
  }

  private string RandomIdentifier(string separator) {
    var count = _random.Next(1, 4);
    return string.Join(separator, Enumerable.Range(0, count).Select(_ => Words[_random.Next(Words.Length)]));
  }

  // ---------------------------------------------------------------------------------------------
  // Contents (parallel, deterministic per file)

  private static byte[] GenerateFile(FilePlan file) {
    var random = new Random(file.Seed);
    if (file.Kind == Kind.Binary) {
      var bytes = new byte[file.Size];
      random.NextBytes(bytes);
      bytes[0] = 0x89; // PNG-like header with NUL bytes.
      bytes[Math.Min(1, bytes.Length - 1)] = 0;
      return bytes;
    }

    var writer = new ContentWriter(random, file.Kind);
    var lines = writer.Generate(file.Size);
    if (file.NonAscii) {
      for (var i = 0; i < 3; i++) {
        lines.Insert(random.Next(lines.Count + 1), writer.Comment(NonAsciiLines[random.Next(NonAsciiLines.Length)]));
      }
    }
    foreach (var marker in file.Markers) {
      var index = marker == LargeFileMarker ? lines.Count : random.Next(lines.Count + 1);
      lines.Insert(index, writer.Comment($"{marker} planted by the VsChromium code base generator"));
    }
    if (file.Markers.Count > 0) {
      KeepMarkersAwayFromChunkBoundaries(lines, file.Crlf ? 2 : 1, file.Utf8Bom ? 3 : 0);
    }

    var text = string.Join(file.Crlf ? "\r\n" : "\n", lines) + (file.Crlf ? "\r\n" : "\n");
    var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: file.Utf8Bom);
    var preamble = encoding.GetPreamble();
    var result = new byte[preamble.Length + encoding.GetByteCount(text)];
    preamble.CopyTo(result, 0);
    encoding.GetBytes(text, 0, text.Length, result, preamble.Length);
    return result;
  }

  /// <summary>
  /// The current server splits files in 100 KB chunks and misses matches spanning two chunks (a
  /// known bug, covered by dedicated regression tests). Planted markers are kept at least 16 bytes
  /// away from multiples of 100 KB (in UTF-8 bytes, with or without the BOM) by inserting padding
  /// lines, so that the expected marker counts are valid for any server.
  /// </summary>
  private static void KeepMarkersAwayFromChunkBoundaries(List<string> lines, int newLineLength, int bomLength) {
    const int chunkSize = 100 * 1024;
    const int margin = 16;
    for (var iteration = 0; iteration < 1000; iteration++) {
      var moved = false;
      long offset = 0;
      for (var i = 0; i < lines.Count; i++) {
        var length = Encoding.UTF8.GetByteCount(lines[i]);
        if (lines[i].Contains(MarkerPlantedSuffix, StringComparison.Ordinal)) {
          // Window around the marker line that must not contain a chunk boundary.
          var start = Math.Max(0, offset - margin);
          var end = offset + bomLength + length + margin;
          var nextBoundary = (start / chunkSize + 1) * chunkSize;
          if (nextBoundary < end) {
            // Insert a padding line so that the marker line starts after the boundary (+ margin).
            lines.Insert(i, new string(' ', (int)(nextBoundary + margin - offset)));
            moved = true;
            break;
          }
        }
        offset += length + newLineLength;
      }
      if (!moved) {
        return;
      }
    }
  }

  private const string MarkerPlantedSuffix = " planted by the VsChromium code base generator";

  private sealed class ContentWriter(Random random, Kind kind) {
    private readonly List<string> _lines = new();
    private int _size;

    public List<string> Generate(int targetSize) {
      WriteHeader();
      while (_size < targetSize) {
        WriteBlock();
      }
      return _lines;
    }

    public string Comment(string text) => kind switch {
      Kind.Python or Kind.Gn or Kind.Text => "# " + text,
      Kind.Html => "<!-- " + text + " -->",
      Kind.Markdown => text,
      _ => "// " + text,
    };

    private void Line(string line = "") {
      _lines.Add(line);
      _size += line.Length + 1;
    }

    private string Word() => Words[random.Next(Words.Length)];
    private string Pascal(int n = 2) => string.Concat(Enumerable.Range(0, n).Select(_ => Capitalize(Word())));
    private string Snake(int n = 2) => string.Join("_", Enumerable.Range(0, n).Select(_ => Word()));
    private string Camel(int n = 2) => Word() + Pascal(n - 1);
    private static string Capitalize(string w) => char.ToUpperInvariant(w[0]) + w[1..];
    private string Sentence() => Capitalize(string.Join(" ", Enumerable.Range(0, random.Next(5, 14)).Select(_ => Word()))) + ".";

    private void WriteHeader() {
      switch (kind) {
        case Kind.Cpp or Kind.Header or Kind.ObjC or Kind.Java or Kind.JavaScript or Kind.TypeScript or Kind.Mojom or Kind.Proto:
          Line($"// Copyright {random.Next(2010, 2027)} The Chromium Authors");
          Line("// Use of this source code is governed by a BSD-style license that can be");
          Line("// found in the LICENSE file.");
          Line();
          break;
        case Kind.Python or Kind.Gn:
          Line($"# Copyright {random.Next(2010, 2027)} The Chromium Authors");
          Line("# Use of this source code is governed by a BSD-style license that can be");
          Line("# found in the LICENSE file.");
          Line();
          break;
      }
      switch (kind) {
        case Kind.Cpp or Kind.Header or Kind.ObjC:
          for (var i = random.Next(2, 10); i > 0; i--) {
            Line($"#include \"{Snake(1)}/{Snake(1)}/{Snake(2)}.h\"");
          }
          Line("#include <memory>");
          Line("#include <string>");
          Line();
          Line($"namespace {Snake(1)} {{");
          Line();
          break;
        case Kind.Java:
          Line($"package org.chromium.{Word()}.{Word()};");
          Line();
          Line($"import org.chromium.base.{Pascal()};");
          Line();
          Line($"public class {Pascal(3)} {{");
          break;
        case Kind.Python:
          Line("import os");
          Line($"import {Snake(1)}");
          Line();
          break;
        case Kind.Mojom or Kind.Proto:
          Line($"module {Word()}.mojom;");
          Line();
          break;
        case Kind.Html:
          Line("<!DOCTYPE html>");
          Line($"<title>{Sentence()}</title>");
          break;
        case Kind.Markdown:
          Line($"# {Capitalize(Snake(2).Replace('_', ' '))}");
          Line();
          break;
        case Kind.Json:
          Line("{");
          break;
      }
    }

    private void WriteBlock() {
      switch (kind) {
        case Kind.Cpp or Kind.Header or Kind.ObjC:
          WriteCppBlock();
          break;
        case Kind.Java:
          Line($"  // {Sentence()}");
          Line($"  public {(random.Next(2) == 0 ? "void" : "int")} {Camel(3)}({Pascal(1)} {Camel(1)}, int {Camel(1)}) {{");
          for (var i = random.Next(1, 8); i > 0; i--) {
            Line($"    m{Pascal(2)}.{Camel(2)}({Camel(1)}, {random.Next(100)});");
          }
          Line("  }");
          Line();
          break;
        case Kind.Python:
          Line($"def {Snake(3)}({Snake(1)}, {Snake(1)}=None):");
          Line($"  \"\"\"{Sentence()}\"\"\"");
          for (var i = random.Next(1, 8); i > 0; i--) {
            Line($"  {Snake(1)} = {Snake(2)}({Snake(1)}, '{Word()}')");
          }
          Line($"  return {Snake(1)}");
          Line();
          break;
        case Kind.Gn:
          Line($"source_set(\"{Snake(2)}\") {{");
          Line("  sources = [");
          for (var i = random.Next(2, 12); i > 0; i--) {
            Line($"    \"{Snake(2)}.cc\",");
          }
          Line("  ]");
          Line($"  deps = [ \"//{Word()}\", \"//{Word()}/{Word()}\" ]");
          Line("}");
          Line();
          break;
        case Kind.Mojom or Kind.Proto:
          Line($"// {Sentence()}");
          Line($"interface {Pascal(2)} {{");
          for (var i = random.Next(1, 6); i > 0; i--) {
            Line($"  {Pascal(2)}(string {Snake(1)}, int32 {Snake(1)}) => (bool success);");
          }
          Line("};");
          Line();
          break;
        case Kind.JavaScript or Kind.TypeScript:
          Line($"// {Sentence()}");
          Line($"export function {Camel(3)}({Camel(1)}, {Camel(1)}) {{");
          for (var i = random.Next(1, 8); i > 0; i--) {
            Line($"  const {Camel(2)} = {Camel(1)}.{Camel(2)}('{Word()}', {random.Next(1000)});");
          }
          Line("}");
          Line();
          break;
        case Kind.Json:
          Line($"  \"{Snake(2)}\": {{ \"{Word()}\": \"{Sentence()}\", \"{Word()}\": {random.Next(10000)} }},");
          break;
        case Kind.Html:
          Line($"<div class=\"{Word()}-{Word()}\" id=\"{Camel(2)}\">");
          Line($"  <span>{Sentence()}</span>");
          Line("</div>");
          break;
        default:
          Line(string.Join(" ", Enumerable.Range(0, random.Next(2, 6)).Select(_ => Sentence())));
          Line();
          break;
      }
    }

    private void WriteCppBlock() {
      var className = Pascal(3);
      switch (random.Next(3)) {
        case 0:
          Line($"// {Sentence()}");
          Line($"class {className} : public {Pascal(2)}::Observer {{");
          Line(" public:");
          Line($"  {className}();");
          Line($"  ~{className}() override;");
          for (var i = random.Next(1, 6); i > 0; i--) {
            Line($"  void {Pascal(2)}(const std::string& {Snake(1)}, int {Snake(1)});");
          }
          Line(" private:");
          Line($"  std::unique_ptr<{Pascal(2)}> {Snake(2)}_;");
          Line("};");
          Line();
          break;
        case 1:
          Line($"void {className}::{Pascal(2)}(const std::string& {Snake(1)}, int count) {{");
          for (var i = random.Next(2, 12); i > 0; i--) {
            switch (random.Next(4)) {
              case 0: Line($"  if (!{Snake(2)}_) {{ return; }}"); break;
              case 1: Line($"  {Snake(2)}_->{Pascal(2)}({Snake(1)}, count + {random.Next(10)});"); break;
              case 2: Line($"  // {Sentence()}"); break;
              default: Line($"  for (int i = 0; i < count; ++i) {{ {Snake(1)}.push_back(i); }}"); break;
            }
          }
          Line("}");
          Line();
          break;
        default:
          Line($"constexpr int k{Pascal(2)} = {random.Next(100000)};");
          Line($"constexpr char k{Pascal(2)}[] = \"{Snake(3)}\";");
          Line();
          break;
      }
    }
  }
}
