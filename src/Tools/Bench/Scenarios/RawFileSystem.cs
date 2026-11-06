// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using VsChromium.Bench.Platform;

namespace VsChromium.Bench.Scenarios;

/// <summary>Timings of one pass of the raw file system scenario.</summary>
public sealed record RawFileSystemTimings(
  int DirectoryCount, double ListMicroseconds, long ListedEntries,
  int FileCount, double AttributesMicroseconds, double ReadMicroseconds, long ReadBytes,
  double ParallelListSeconds);

/// <summary>
/// Scenario 7: the cost of the file system itself, without the server, on a sample of the corpus.
/// Isolates the antivirus and filter driver costs from the server's code:
/// <list type="bullet">
/// <item>directory listing (per directory);</item>
/// <item>attribute-only open and close of files (Windows: CreateFile with FILE_READ_ATTRIBUTES;
/// elsewhere: stat);</item>
/// <item>open, read and close of files;</item>
/// <item>listing of all the directories of the corpus on all the cores (as the server's scan).</item>
/// </list>
/// </summary>
public sealed class RawFileSystem {
  private readonly List<string> _directories;
  private readonly List<string> _files;
  private readonly List<string> _allDirectories;

  private RawFileSystem(List<string> directories, List<string> files, List<string> allDirectories) {
    _directories = directories;
    _files = files;
    _allDirectories = allDirectories;
  }

  public int DirectorySampleCount => _directories.Count;
  public int FileSampleCount => _files.Count;

  /// <summary>Deterministic samples: directories and files at regular intervals of the sorted lists.</summary>
  public static RawFileSystem Prepare(string root, int directorySample = 5_000, int fileSample = 20_000) {
    var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
    var directories = Directory.EnumerateDirectories(root, "*", options).ToList();
    directories.Add(root);
    directories.Sort(StringComparer.Ordinal);
    var files = Directory.EnumerateFiles(root, "*", options).ToList();
    files.Sort(StringComparer.Ordinal);
    return new RawFileSystem(Sample(directories, directorySample), Sample(files, fileSample), directories);
  }

  private static List<string> Sample(List<string> items, int count) {
    if (items.Count <= count) {
      return items.ToList();
    }
    var stride = (double)items.Count / count;
    return Enumerable.Range(0, count).Select(i => items[(int)(i * stride)]).ToList();
  }

  public RawFileSystemTimings Run() {
    var stopwatch = Stopwatch.StartNew();
    long entries = 0;
    foreach (var dir in _directories) {
      foreach (var _ in Directory.EnumerateFileSystemEntries(dir)) {
        entries++;
      }
    }
    var listUs = stopwatch.Elapsed.TotalMicroseconds / Math.Max(1, _directories.Count);

    stopwatch.Restart();
    foreach (var file in _files) {
      OpenAttributesOnly(file);
    }
    var attributesUs = stopwatch.Elapsed.TotalMicroseconds / Math.Max(1, _files.Count);

    var buffer = new byte[64 * 1024];
    long bytes = 0;
    stopwatch.Restart();
    foreach (var file in _files) {
      using var handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
      long offset = 0;
      int read;
      while ((read = RandomAccess.Read(handle, buffer, offset)) > 0) {
        offset += read;
      }
      bytes += offset;
    }
    var readUs = stopwatch.Elapsed.TotalMicroseconds / Math.Max(1, _files.Count);

    stopwatch.Restart();
    long parallelEntries = 0;
    Parallel.ForEach(_allDirectories, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, dir => {
      long count = 0;
      foreach (var _ in Directory.EnumerateFileSystemEntries(dir)) {
        count++;
      }
      Interlocked.Add(ref parallelEntries, count);
    });
    var parallelSeconds = stopwatch.Elapsed.TotalSeconds;

    return new RawFileSystemTimings(_directories.Count, listUs, entries, _files.Count, attributesUs, readUs, bytes, parallelSeconds);
  }

  private static void OpenAttributesOnly(string path) {
    if (OperatingSystem.IsWindows()) {
      using var handle = WindowsNative.OpenForAttributes(path);
      if (handle.IsInvalid) {
        throw new IOException($"Cannot open \"{path}\" (error {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}).");
      }
    } else {
      _ = File.GetLastWriteTimeUtc(path);
    }
  }
}
