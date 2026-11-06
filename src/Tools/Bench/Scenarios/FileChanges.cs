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

  private FileChanges(List<string> candidates) {
    _candidates = candidates;
  }

  public int CandidateCount => _candidates.Count;

  /// <summary>
  /// The text files of the corpus (not in "out/", not links, smaller than 1 MB), sorted by path:
  /// the same list on every machine for the same corpus.
  /// </summary>
  public static FileChanges Scan(string root) {
    var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
    var outDir = Path.Combine(root, "out") + Path.DirectorySeparatorChar;
    var files = new List<string>();
    foreach (var info in new DirectoryInfo(root).EnumerateFiles("*", options)) {
      if (TextExtensions.Contains(info.Extension) && info.Length < 1 << 20 && !info.FullName.StartsWith(outDir, StringComparison.Ordinal)) {
        files.Add(info.FullName);
      }
    }
    files.Sort(StringComparer.Ordinal);
    return new FileChanges(files);
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
