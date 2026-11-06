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
///
/// Version 2 (changes from version 1):
/// <list type="bullet">
/// <item>The number of files per directory follows the distribution measured on a large real project
/// (<see cref="FilesPerDirectoryBuckets"/>): a quarter of the directories without files
/// (intermediate directories, e.g. Java-like package chains), most with 1 to 4 files, a few with
/// hundreds or thousands (test data, generated files). About 4 files per directory on average.</item>
/// <item>A fixed number of very large text files (<see cref="VeryLargeFileCount"/>, 2-16 MB) whatever
/// the corpus size, instead of 0.1% of the text files.</item>
/// <item>Optional symbolic links (<see cref="CorpusSpec.Symlinks"/>): file links and directory links
/// to generated files and directories without markers, added after everything else (the rest of
/// the corpus is identical with or without links).</item>
/// </list>
/// </summary>
public sealed class CodeBaseGenerator(CorpusSpec spec) {
  public const int Version = 2;

  // Markers planted in the code base (see CorpusManifest.Markers for the exact counts).
  public const string UniqueMarkerPrefix = "VSCB_UNIQUE_MARKER_";
  public const string RareMarker = "VSCB_RARE_MARKER";
  public const string CommonMarker = "VSCB_COMMON_MARKER";
  public const string LargeFileMarker = "VSCB_LARGE_FILE_MARKER";
  public const int UniqueMarkerCount = 10;

  /// <summary>Very large (generated-like) text files of 2-16 MB, whatever the size of the corpus.</summary>
  public const int VeryLargeFileCount = 20;

  /// <summary>Maximum number of files of a directory.</summary>
  public const int MaxFilesPerDirectory = 3_000;

  private const int MaxDepth = 9;

  /// <summary>
  /// Distribution of the number of files per directory (source directories, links excluded), as
  /// measured on a large real project (289,637 directories, 1,177,290 files): bucket bounds
  /// (inclusive) and fraction of the directories. Within a bucket, counts are log-uniform.
  /// </summary>
  public static readonly IReadOnlyList<(int Min, int Max, double DirectoryFraction)> FilesPerDirectoryBuckets = [
    (0, 0, 0.255), (1, 1, 0.388), (2, 4, 0.206), (5, 9, 0.085), (10, 19, 0.037), (20, 49, 0.020),
    (50, 99, 0.005), (100, 249, 0.003), (250, 499, 0.0006), (500, 999, 0.0003), (1000, MaxFilesPerDirectory, 0.0002),
  ];

  /// <summary>Average number of files of a directory with the distribution of <see cref="FilesPerDirectoryBuckets"/>.</summary>
  private static double ExpectedFilesPerDirectory => FilesPerDirectoryBuckets.Sum(b => b.DirectoryFraction * LogUniformMean(b.Min, b.Max));

  private static double LogUniformMean(int min, int max) =>
    min == max ? min : (max + 1 - min) / Math.Log((max + 1.0) / min);

  private enum Kind { Cpp, Header, ObjC, Python, Gn, Mojom, Java, JavaScript, TypeScript, Json, Html, Markdown, Text, Proto, Binary }

  private sealed record FilePlan(string RelativePath, Kind Kind, int Size, int Seed, bool Utf8Bom, bool Crlf, bool NonAscii) {
    /// <summary>Settable: some files become very large (see <see cref="PlanVeryLargeFiles"/>).</summary>
    public int Size { get; set; } = Size;
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

  /// <summary>The plan of the corpus (no disk access): directories, files, markers, links.</summary>
  private sealed record CorpusPlan(
    List<string> Directories,
    List<FilePlan> Files,
    List<PlantedMarker> Markers,
    List<(string Link, string Target)> FileLinks,
    List<(string Link, string Target, int FileCount)> DirectoryLinks);

  private CorpusPlan? _plan;

  private CorpusPlan Plan() {
    if (_plan != null) {
      return _plan;
    }
    var directories = PlanDirectories();
    var files = PlanFiles(directories);
    PlanVeryLargeFiles(files);
    var markers = PlantMarkers(files);
    var (fileLinks, directoryLinks) = spec.Symlinks ? PlanSymlinks(directories, files) : ([], []);
    return _plan = new CorpusPlan(directories, files, markers, fileLinks, directoryLinks);
  }

  /// <summary>
  /// The distribution of the number of files per directory of the corpus (source directories:
  /// "out/" and links excluded), computed from the plan only (no disk access).
  /// </summary>
  public List<FilesPerDirectoryBucket> ComputeFilesPerDirectoryHistogram() {
    var plan = Plan();
    return ComputeHistogram(plan.Directories, plan.Files);
  }

  public CorpusManifest Generate(string rootPath, Action<string>? log = null) {
    log ??= _ => { };
    var plan = Plan();
    var (directories, files, markers) = (plan.Directories, plan.Files, plan.Markers);

    Directory.CreateDirectory(rootPath);
    foreach (var dir in directories) {
      Directory.CreateDirectory(Path.Combine(rootPath, dir));
    }
    File.WriteAllText(Path.Combine(rootPath, "vs-chromium-project.txt"), ProjectFileContents);

    long totalBytes = 0;
    var written = 0;
    var nextReport = files.Count / 10;
    var reportLock = new object();
    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file => {
      var bytes = GenerateFile(file);
      File.WriteAllBytes(Path.Combine(rootPath, file.RelativePath), bytes);
      Interlocked.Add(ref totalBytes, bytes.Length);
      var count = Interlocked.Increment(ref written);
      if (count >= nextReport && files.Count >= 10_000) {
        lock (reportLock) {
          if (count >= nextReport) {
            log($"  {count * 100L / files.Count}% ({count:n0} files)");
            nextReport += files.Count / 10;
          }
        }
      }
    });

    var symlinks = "none";
    int fileLinkCount = 0, directoryLinkCount = 0, linkedFileCount = 0;
    if (spec.Symlinks) {
      var reason = CanCreateSymlinks(rootPath);
      if (reason != null) {
        symlinks = $"skipped ({reason})";
        log($"Symbolic links skipped: {reason}");
      } else {
        foreach (var (link, target) in plan.FileLinks) {
          File.CreateSymbolicLink(Path.Combine(rootPath, link), target);
        }
        foreach (var (link, target, fileCount) in plan.DirectoryLinks) {
          Directory.CreateSymbolicLink(Path.Combine(rootPath, link), target);
          linkedFileCount += fileCount;
        }
        fileLinkCount = plan.FileLinks.Count;
        directoryLinkCount = plan.DirectoryLinks.Count;
        linkedFileCount += fileLinkCount;
        symlinks = "created";
      }
    }

    return new CorpusManifest(
      GeneratorVersion: Version,
      Spec: spec,
      FileCount: files.Count,
      DirectoryCount: directories.Count,
      TotalBytes: totalBytes,
      SearchableFileCount: files.Count(f => f.Kind != Kind.Binary && !IsUnderOut(f.RelativePath)),
      CreatedUtc: DateTime.UtcNow,
      Markers: markers) {
      Symlinks = symlinks,
      SymlinkFileCount = fileLinkCount,
      SymlinkDirectoryCount = directoryLinkCount,
      FilesThroughSymlinksCount = linkedFileCount,
      FilesPerDirectoryHistogram = ComputeHistogram(directories, files),
    };
  }

  /// <summary>Null if symbolic links can be created in <paramref name="directory"/>, otherwise the reason.</summary>
  public static string? CanCreateSymlinks(string directory) {
    var probe = Path.Combine(directory, ".vschromium-symlink-probe-" + Environment.ProcessId);
    try {
      File.CreateSymbolicLink(probe, "probe-target");
      return null;
    } catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException) {
      return OperatingSystem.IsWindows()
        ? $"{e.Message.TrimEnd('.', ' ')}: enable the Developer Mode of Windows, or run elevated"
        : e.Message.TrimEnd('.', ' ');
    } finally {
      try {
        File.Delete(probe);
      } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        // Best effort
      }
    }
  }

  /// <summary>Project file: "out/" is ignored (as in Chromium), binary files are not searchable.</summary>
  private const string ProjectFileContents =
    "[SourceExplorer.ignore]\nout/\n.git/\n\n" +
    "[SearchableFiles.include]\n*\n\n" +
    "[SearchableFiles.ignore]\n*.png\n*.zip\n*.pak\n*.o\n*.obj\n";

  private static bool IsUnderOut(string relativePath) => relativePath.StartsWith("out" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

  private static List<FilesPerDirectoryBucket> ComputeHistogram(List<string> directories, List<FilePlan> files) {
    var counts = directories.Where(d => !IsUnderOut(d + Path.DirectorySeparatorChar) && d != "out").ToDictionary(d => d, _ => 0);
    foreach (var file in files) {
      var dir = Path.GetDirectoryName(file.RelativePath) ?? "";
      if (counts.TryGetValue(dir, out var count)) {
        counts[dir] = count + 1;
      }
    }
    var totalDirs = Math.Max(1, counts.Count);
    var totalFiles = Math.Max(1, counts.Values.Sum());
    return FilesPerDirectoryBuckets.Select(b => {
      var inBucket = counts.Values.Where(c => c >= b.Min && (c <= b.Max || b.Max == MaxFilesPerDirectory)).ToList();
      return new FilesPerDirectoryBucket(
        b.Min == b.Max ? $"{b.Min}" : $"{b.Min}-{b.Max}",
        inBucket.Count, inBucket.Sum(),
        Math.Round(100.0 * inBucket.Count / totalDirs, 2), Math.Round(100.0 * inBucket.Sum() / totalFiles, 2));
    }).ToList();
  }

  // ---------------------------------------------------------------------------------------------
  // Planning (sequential, deterministic)

  /// <summary>Files of the build output directory (ignored by the project file).</summary>
  private int OutFileCount => spec.FileCount / 50;

  /// <summary>
  /// The directory tree: a random tree with a maximum depth, about <c>FileCount / 4.4</c> source
  /// directories (see <see cref="ExpectedFilesPerDirectory"/>), with Java-like chains of
  /// single-subdirectory directories. Half of the new directories go under a uniformly chosen
  /// directory, half under the parent of a uniformly chosen directory (preferential attachment:
  /// many leaves, a few directories with many subdirectories).
  /// </summary>
  private List<string> PlanDirectories() {
    var dirs = new List<(string Path, int Depth)>();
    var children = new Dictionary<string, HashSet<string>>();
    var parents = new Dictionary<string, string>();
    foreach (var top in TopLevelDirectories) {
      dirs.Add((top, 1));
    }
    var sourceTarget = Math.Max(30, (int)Math.Round((spec.FileCount - OutFileCount) / ExpectedFilesPerDirectory));
    var depthOf = dirs.ToDictionary(d => d.Path, d => d.Depth);

    bool TryAdd(string parentPath, int parentDepth, out string path) {
      path = "";
      if (parentDepth >= MaxDepth) {
        return false;
      }
      var name = DirectoryWords[_random.Next(DirectoryWords.Length)];
      if (!children.TryGetValue(parentPath, out var siblings)) {
        children[parentPath] = siblings = new HashSet<string>();
      }
      if (!siblings.Add(name)) {
        name = name + "_" + Words[_random.Next(Words.Length)];
        if (!siblings.Add(name)) {
          return false;
        }
      }
      path = Path.Combine(parentPath, name);
      dirs.Add((path, parentDepth + 1));
      depthOf[path] = parentDepth + 1;
      parents[path] = parentPath;
      return true;
    }

    while (dirs.Count < sourceTarget) {
      var pick = dirs[_random.Next(dirs.Count)];
      if (_random.NextDouble() < 0.5 && parents.TryGetValue(pick.Path, out var parentOfPick)) {
        pick = (parentOfPick, depthOf[parentOfPick]);
      }
      if (!TryAdd(pick.Path, pick.Depth, out var path)) {
        continue;
      }
      // Java-like package chains ("java/src/org/chromium/..."): single-subdirectory directories.
      if (_random.NextDouble() < 0.04) {
        var depth = pick.Depth + 1;
        for (var i = _random.Next(2, 5); i > 0 && dirs.Count < sourceTarget; i--) {
          if (!TryAdd(path, depth, out var next)) {
            break;
          }
          path = next;
          depth++;
        }
      }
    }
    dirs.Add(("out", 1));
    dirs.Add((Path.Combine("out", "Default"), 2));
    dirs.Add((Path.Combine("out", "Default", "obj"), 3));
    return dirs.Select(d => d.Path).ToList();
  }

  /// <summary>
  /// The number of files of each source directory, drawn from <see cref="FilesPerDirectoryBuckets"/>
  /// (directories without files are preferably intermediate directories), adjusted within the
  /// buckets so that the total is exactly the number of source files.
  /// </summary>
  private Dictionary<string, int> PlanFilesPerDirectory(List<string> sourceDirs, int totalFiles) {
    var hasChildren = new HashSet<string>(sourceDirs.Select(d => Path.GetDirectoryName(d) ?? "").Where(p => p.Length > 0));
    var zeroCount = (int)Math.Round(FilesPerDirectoryBuckets[0].DirectoryFraction * sourceDirs.Count);
    // Intermediate directories first (in random order), then leaves.
    var order = sourceDirs.Select(d => (Dir: d, Key: (hasChildren.Contains(d) ? 0 : 1) + _random.NextDouble())).OrderBy(x => x.Key).Select(x => x.Dir).ToList();
    var counts = new Dictionary<string, int>();
    var buckets = new Dictionary<string, int>();
    var nonZeroBuckets = FilesPerDirectoryBuckets.Skip(1).ToList();
    var nonZeroTotal = nonZeroBuckets.Sum(b => b.DirectoryFraction);
    for (var i = 0; i < order.Count; i++) {
      if (i < zeroCount) {
        counts[order[i]] = 0;
        buckets[order[i]] = 0;
        continue;
      }
      var r = _random.NextDouble() * nonZeroTotal;
      var bucket = 0;
      while (bucket < nonZeroBuckets.Count - 1 && r >= nonZeroBuckets[bucket].DirectoryFraction) {
        r -= nonZeroBuckets[bucket].DirectoryFraction;
        bucket++;
      }
      var (min, max, _) = nonZeroBuckets[bucket];
      var value = (int)Math.Floor(Math.Exp(Math.Log(min) + _random.NextDouble() * (Math.Log(max + 1.0) - Math.Log(min))));
      counts[order[i]] = Math.Clamp(value, min, max);
      buckets[order[i]] = bucket + 1;
    }

    // Exact total: add or remove files in random directories, within their bucket.
    var nonZeroDirs = order.Skip(zeroCount).ToList();
    var diff = totalFiles - counts.Values.Sum();
    for (var attempts = 0; diff != 0 && attempts < 100 * (Math.Abs(diff) + nonZeroDirs.Count); attempts++) {
      var dir = nonZeroDirs[_random.Next(nonZeroDirs.Count)];
      var (min, max, _) = FilesPerDirectoryBuckets[buckets[dir]];
      if (diff > 0 && counts[dir] < max) {
        var add = Math.Min(diff, Math.Max(1, (max - counts[dir]) / 4));
        counts[dir] += add;
        diff -= add;
      } else if (diff < 0 && counts[dir] > min) {
        var remove = Math.Min(-diff, Math.Max(1, (counts[dir] - min) / 4));
        counts[dir] -= remove;
        diff += remove;
      }
    }
    if (diff > 0) {
      // Not reachable with realistic sizes: the rest in the largest directories.
      counts[nonZeroDirs.OrderByDescending(d => counts[d]).First()] += diff;
    }
    return counts;
  }

  private List<FilePlan> PlanFiles(List<string> directories) {
    var files = new List<FilePlan>();
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var sourceDirs = directories.Where(d => !IsUnderOut(d + Path.DirectorySeparatorChar) && d != "out").ToList();
    var outObjDir = Path.Combine("out", "Default", "obj");
    var totalWeight = Extensions.Sum(e => e.Weight);
    var outFiles = OutFileCount;
    var perDirectory = PlanFilesPerDirectory(sourceDirs, spec.FileCount - outFiles);

    foreach (var dir in sourceDirs) {
      var target = perDirectory[dir];
      if (target == 0) {
        continue;
      }
      // Large directories look like test data or generated files: most files have the same
      // extension and numbered names.
      var dominant = target >= 50 ? DominantExtensions[_random.Next(DominantExtensions.Length)] : default;
      var dominantPrefix = target >= 50 ? RandomIdentifier("_") : "";
      var start = files.Count;
      var serial = 0;
      for (var attempts = 0; files.Count - start < target && attempts < target * 20; attempts++) {
        Kind kind;
        string ext, baseName;
        if (dominant.Ext != null && _random.NextDouble() < 0.8) {
          (kind, ext) = dominant;
          baseName = $"{dominantPrefix}_{serial++:D4}";
        } else {
          (kind, ext) = PickExtension(totalWeight);
          baseName = RandomIdentifier("_") + FileSuffixes[_random.Next(FileSuffixes.Length)];
          if (kind is Kind.Cpp && _random.NextDouble() < 0.15) {
            baseName += "_unittest";
          }
          if (kind is Kind.Gn) {
            baseName = ext == ".gn" ? "BUILD" : baseName;
          }
        }
        var path = Path.Combine(dir, baseName + ext);
        if (!names.Add(path)) {
          continue;
        }
        files.Add(NewFile(path, kind));
        // Most .cc files come with a header.
        if (kind == Kind.Cpp && files.Count - start < target && !baseName.EndsWith("_unittest", StringComparison.Ordinal) && _random.NextDouble() < 0.7) {
          var header = Path.Combine(dir, baseName + ".h");
          if (names.Add(header)) {
            files.Add(NewFile(header, Kind.Header));
          }
        }
      }
    }

    // ~2% of the files are build outputs (ignored by the project file).
    for (var i = 0; i < outFiles; i++) {
      files.Add(NewFile(Path.Combine(outObjDir, $"{RandomIdentifier("_")}_{i}.obj"), Kind.Binary));
    }
    return files;
  }

  /// <summary>Extensions of the large directories (test data, generated files).</summary>
  private static readonly (Kind Kind, string Ext)[] DominantExtensions = [
    (Kind.Json, ".json"), (Kind.Text, ".txt"), (Kind.Html, ".html"), (Kind.Binary, ".png"), (Kind.JavaScript, ".js"),
    (Kind.Header, ".h"), (Kind.Cpp, ".cc"), (Kind.Java, ".java"), (Kind.Python, ".py"), (Kind.Markdown, ".md"),
  ];

  private FilePlan NewFile(string path, Kind kind) {
    // Log-normal distribution: median ~5 KB, long tail.
    var normal = Math.Sqrt(-2 * Math.Log(1 - _random.NextDouble())) * Math.Cos(2 * Math.PI * _random.NextDouble());
    var size = (int)Math.Clamp(Math.Exp(Math.Log(5000) + 1.1 * normal), 64, 1_500_000);
    var text = kind != Kind.Binary;
    return new FilePlan(path, kind, size, _random.Next(),
      Utf8Bom: text && _random.NextDouble() < 0.005,
      Crlf: text && _random.NextDouble() < 0.05,
      NonAscii: text && _random.NextDouble() < 0.03);
  }

  /// <summary>
  /// <see cref="VeryLargeFileCount"/> text files (of the kinds where markers are planted) become very
  /// large (generated-like) files of 2-16 MB.
  /// </summary>
  private void PlanVeryLargeFiles(List<FilePlan> files) {
    var candidates = files.Where(f => IsMarkerEligible(f)).ToList();
    foreach (var file in candidates.OrderBy(_ => _random.Next()).Take(VeryLargeFileCount).ToList()) {
      file.Size = _random.Next(2 << 20, 16 << 20);
    }
  }

  private static bool IsMarkerEligible(FilePlan f) =>
    f.Kind is Kind.Cpp or Kind.Header or Kind.Python or Kind.Java or Kind.Markdown or Kind.Text && !IsUnderOut(f.RelativePath);

  /// <summary>
  /// Symbolic links (relative targets): about one file link per 1,000 files (10 to 300) to text files
  /// without markers, and 5 directory links to leaf directories whose files have no markers. Planned
  /// with a separate random generator: the rest of the corpus doesn't depend on links.
  /// </summary>
  private (List<(string, string)>, List<(string, string, int)>) PlanSymlinks(List<string> directories, List<FilePlan> files) {
    var random = new Random(spec.Seed ^ 0x51ED);
    var names = new HashSet<string>(files.Select(f => f.RelativePath).Concat(directories), StringComparer.OrdinalIgnoreCase);
    var sourceDirs = directories.Where(d => !IsUnderOut(d + Path.DirectorySeparatorChar) && d != "out").ToList();

    // Directory links: to leaf directories with 1-20 files, without markers or very large files.
    var parentsOfDirs = new HashSet<string>(sourceDirs.Select(d => Path.GetDirectoryName(d) ?? ""));
    var filesByDir = files.GroupBy(f => Path.GetDirectoryName(f.RelativePath) ?? "").ToDictionary(g => g.Key, g => g.ToList());
    var leafTargets = sourceDirs
      .Where(d => !parentsOfDirs.Contains(d) && filesByDir.TryGetValue(d, out var list) && list.Count is >= 1 and <= 20 &&
                  list.All(f => f.Markers.Count == 0 && f.Size < 1 << 20))
      .ToList();
    var directoryLinks = new List<(string, string, int)>();
    var linkedDirs = new HashSet<string>();
    for (var attempts = 0; directoryLinks.Count < 5 && leafTargets.Count > 0 && attempts < 100; attempts++) {
      var target = leafTargets[random.Next(leafTargets.Count)];
      var dir = sourceDirs[random.Next(sourceDirs.Count)];
      // No link in a linked directory (no cycle, files seen through links counted exactly).
      if (dir == target || linkedDirs.Contains(dir) || linkedDirs.Contains(target) ||
          directoryLinks.Any(l => Path.GetDirectoryName(l.Item1) == target)) {
        continue;
      }
      var link = Path.Combine(dir, Path.GetFileName(target) + "_dirlink");
      if (!names.Add(link)) {
        continue;
      }
      linkedDirs.Add(target);
      directoryLinks.Add((link, Path.GetRelativePath(dir, target), filesByDir[target].Count));
    }

    // File links: to text files without markers (not very large), not in linked directories.
    var fileLinks = new List<(string, string)>();
    var targets = files.Where(f => f.Kind != Kind.Binary && f.Markers.Count == 0 && f.Size < 1 << 20 && !IsUnderOut(f.RelativePath)).ToList();
    var fileLinkCount = Math.Clamp(spec.FileCount / 1000, 10, 300);
    for (var attempts = 0; fileLinks.Count < fileLinkCount && targets.Count > 0 && attempts < fileLinkCount * 10; attempts++) {
      var target = targets[random.Next(targets.Count)].RelativePath;
      var dir = sourceDirs[random.Next(sourceDirs.Count)];
      if (linkedDirs.Contains(dir)) {
        continue;
      }
      var link = Path.Combine(dir, Path.GetFileNameWithoutExtension(target) + "_link" + Path.GetExtension(target));
      if (!names.Add(link)) {
        continue;
      }
      fileLinks.Add((link, Path.GetRelativePath(dir, target)));
    }
    return (fileLinks, directoryLinks);
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
    var eligible = files.Where(IsMarkerEligible).ToList();
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
