// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;

namespace VsChromium.Bench.Scenarios;

/// <summary>
/// Deterministic file changes of the corpus: a marker line appended to N text files chosen at regular
/// intervals in the sorted list of the files, then the original contents and times restored (the
/// corpus stays reusable).
/// </summary>
public sealed class FileChanges {
  private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase) {
    ".cc", ".h", ".mm", ".py", ".gn", ".gni", ".mojom", ".java", ".js", ".ts", ".json", ".html", ".md", ".txt", ".proto",
  };

  private readonly List<string> _candidates;
  /// <summary>The number of indexed paths of the candidates seen through symbolic links (1 for the others).</summary>
  private readonly Dictionary<string, int> _visiblePaths;

  private FileChanges(List<string> candidates, Dictionary<string, int> visiblePaths) {
    _candidates = candidates;
    _visiblePaths = visiblePaths;
  }

  /// <summary>
  /// The hits of a marker added to <paramref name="files"/>: one per indexed path of each file (its
  /// path, and the paths of the links that see it).
  /// </summary>
  public int ExpectedHits(IReadOnlyList<string> files) {
    return files.Sum(f => _visiblePaths.TryGetValue(f, out var count) ? count : 1);
  }

  public int CandidateCount => _candidates.Count;

  /// <summary>
  /// The text files of the corpus (not in "out/", not links, smaller than 1 MB), sorted by path:
  /// the same list on every machine for the same corpus. Files seen through symbolic links (targets
  /// of file links, files of linked directories) are excluded: a change of a link target is not a
  /// change of the link for the file system watcher, so the expected number of hits of the marker
  /// would depend on the server's handling of links. With <paramref name="includeLinkTargets"/>,
  /// they are included, and <see cref="ExpectedHits"/> counts the paths of the links that see them.
  /// </summary>
  public static FileChanges Scan(string root, bool includeLinkTargets = false) {
    var outDir = Path.Combine(root, "out") + Path.DirectorySeparatorChar;
    var files = new List<string>();
    var linkedFiles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var linkedDirectories = new List<string>();
    var pending = new Stack<string>();
    pending.Push(root);
    while (pending.Count > 0) {
      var directory = new DirectoryInfo(pending.Pop());
      foreach (var entry in directory.EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 })) {
        if (entry.LinkTarget != null) {
          var target = Path.GetFullPath(Path.Combine(directory.FullName, entry.LinkTarget));
          if (entry is DirectoryInfo) {
            linkedDirectories.Add(target.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
          } else {
            linkedFiles[target] = linkedFiles.GetValueOrDefault(target) + 1;
          }
          continue;
        }
        if (entry is DirectoryInfo) {
          pending.Push(entry.FullName);
        } else if (entry is FileInfo info && TextExtensions.Contains(info.Extension) && info.Length < 1 << 20 &&
                   !info.FullName.StartsWith(outDir, StringComparison.Ordinal)) {
          files.Add(info.FullName);
        }
      }
    }
    var visiblePaths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var file in files) {
      var links = linkedFiles.GetValueOrDefault(file) + linkedDirectories.Count(d => file.StartsWith(d, StringComparison.OrdinalIgnoreCase));
      if (links > 0) {
        visiblePaths[file] = 1 + links;
      }
    }
    if (!includeLinkTargets) {
      files.RemoveAll(visiblePaths.ContainsKey);
      visiblePaths.Clear();
    }
    files.Sort(StringComparer.Ordinal);
    return new FileChanges(files, visiblePaths);
  }

  /// <summary>
  /// <paramref name="count"/> files at regular intervals, starting at a different offset for each
  /// <paramref name="salt"/> (e.g. live vs offline changes don't modify the same files).
  /// </summary>
  public List<string> Select(int count, int salt) {
    count = Math.Min(count, _candidates.Count);
    var result = new List<string>(count);
    if (count == 0) {
      return result;
    }
    var stride = (double)_candidates.Count / count;
    var offset = (int)(stride * ((salt * 0.381966) % 1.0));
    for (var i = 0; i < count; i++) {
      result.Add(_candidates[(offset + (int)(i * stride)) % _candidates.Count]);
    }
    return result.Distinct().ToList();
  }

  /// <summary>The original state of modified files.</summary>
  public sealed class Backup(List<(string Path, byte[] Contents, DateTime LastWriteUtc)> files) {
    public int Count => files.Count;

    /// <summary>Restores the contents and the last write times.</summary>
    public void Restore() {
      foreach (var (path, contents, lastWrite) in files) {
        File.WriteAllBytes(path, contents);
        File.SetLastWriteTimeUtc(path, lastWrite);
      }
    }
  }

  /// <summary>Appends a comment line with <paramref name="marker"/> to each file; returns the backup.</summary>
  public static Backup Modify(IReadOnlyList<string> files, string marker) {
    var backup = new List<(string, byte[], DateTime)>(files.Count);
    var line = Encoding.UTF8.GetBytes($"\n// {marker}\n");
    try {
      foreach (var path in files) {
        var contents = File.ReadAllBytes(path);
        var lastWrite = File.GetLastWriteTimeUtc(path);
        backup.Add((path, contents, lastWrite));
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        stream.Write(line);
      }
    } catch {
      new Backup(backup).Restore();
      throw;
    }
    return new Backup(backup);
  }
}
