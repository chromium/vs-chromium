// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;
using VsChromium.Server.Cache;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestIndexCache {
    private static readonly DateTime LoadStart = DateTime.UtcNow;
    private static readonly DateTime OldTime = LoadStart.AddHours(-1);

    private string _cacheRoot;
    private string _projectRoot;
    private ArenaAllocator _arenas;
    private readonly List<ProjectIndexCache> _caches = new List<ProjectIndexCache>();

    [TestInitialize]
    public void Initialize() {
      _cacheRoot = Path.Combine(Path.GetTempPath(), "VsChromiumIndexCacheTest-" + Guid.NewGuid().ToString("N"));
      _projectRoot = Path.Combine(Path.GetTempPath(), "VsChromiumIndexCacheProject");
      _arenas = new ArenaAllocator();
    }

    [TestCleanup]
    public void Cleanup() {
      foreach (var cache in _caches) {
        cache.Dispose();
      }
      // Release the mapped views before deleting the files.
      GC.Collect();
      GC.WaitForPendingFinalizers();
      try {
        Directory.Delete(_cacheRoot, recursive: true);
      }
      catch (IOException) {
        // Best effort
      }
      catch (UnauthorizedAccessException) {
        // Best effort
      }
    }

    [TestMethod]
    public void ContentsRoundTrip() {
      var items = new[] {
        TextItem("a.cc", "int a;\n"),
        TextItem(Path.Combine("dir", "b.cc"), "// caf\u00e9\n"),
        TextItem("empty.txt", ""),
        BinaryItem("image.png", 1234),
      };
      var result = Write(items);
      Assert.AreEqual(4, result.EntryCount);
      Assert.AreEqual(3, result.NewFileCount);
      Assert.AreEqual(1, result.NewArenaCount);

      var cache = OpenNew();
      Assert.IsNotNull(cache.Lookup);
      Assert.AreEqual(4, cache.Lookup.Count);

      var a = (Utf8FileContents)cache.Lookup.TryGetContents("a.cc", items[0].Metadata);
      Assert.AreEqual("int a;\n", GetText(a));
      Assert.IsTrue(a.IsAscii);
      Assert.AreEqual(OldTime, a.UtcLastModified);
      Assert.IsInstanceOfType(a.Contents.Block, typeof(SafeMemoryMappedViewHandle), "Contents are in a mapped arena file");
      Assert.AreEqual(0, a.Contents.Offset % ArenaAllocator.Alignment);

      var b = (Utf8FileContents)cache.Lookup.TryGetContents(Path.Combine("dir", "b.cc"), items[1].Metadata);
      Assert.AreEqual("// caf\u00e9\n", GetText(b));
      Assert.IsFalse(b.IsAscii);

      var empty = cache.Lookup.TryGetContents("empty.txt", items[2].Metadata);
      Assert.AreEqual(0, empty.ByteLength);

      var binary = (BinaryFileContents)cache.Lookup.TryGetContents("image.png", items[3].Metadata);
      Assert.AreEqual(1234, binary.BinaryFileSize);
    }

    [TestMethod]
    public void ManyDirectoriesAndArenasRoundTrip() {
      // Nested directories (front-coded paths), a directory seen twice, files in several arena
      // files (36 x 500 KB > 16 MB), binary files between text files (location deltas).
      var items = new List<Item>();
      var directories = new[] { "", "dir", Path.Combine("dir", "sub"), Path.Combine("dir", "sub", "deeper"), "dir2", "" };
      var large = new string('x', 500 * 1024);
      for (var d = 0; d < directories.Length; d++) {
        for (var f = 0; f < 10; f++) {
          var path = Path.Combine(directories[d], $"file{d}_{f}.cc");
          items.Add(f == 3 ? BinaryItem(path, 100 + f) : TextItem(path, (f % 2 == 0 || f == 7 ? large : "") + $"text {d} {f} caf\u00e9"));
        }
      }
      var result = Write(items);
      Assert.AreEqual(items.Count, result.EntryCount);
      Assert.IsTrue(result.NewArenaCount >= 2, $"{result.NewArenaCount} arenas");

      var lookup = OpenNew().Lookup;
      Assert.AreEqual(items.Count, lookup.Count);
      foreach (var item in items) {
        var contents = lookup.TryGetContents(RelativePath(item), item.Metadata);
        Assert.IsNotNull(contents, RelativePath(item));
        if (item.Contents is BinaryFileContents binary) {
          Assert.AreEqual(binary.BinaryFileSize, ((BinaryFileContents)contents).BinaryFileSize);
        } else {
          Assert.AreEqual(GetText(item.Contents), GetText(contents), RelativePath(item));
        }
      }
    }

    [TestMethod]
    public void IndexIsCompact() {
      var items = new List<Item>();
      for (var d = 0; d < 20; d++) {
        for (var f = 0; f < 100; f++) {
          items.Add(TextItem(Path.Combine("src", "components", $"directory{d:D2}", $"file_name_{f:D3}.cc"), $"int f{d}_{f}; // some text"));
        }
      }
      Write(items);
      var bytesPerFile = (double)new FileInfo(IndexPath).Length / items.Count;
      // Name (~15 bytes) + flags, time, size, location deltas.
      Assert.IsTrue(bytesPerFile < 30, $"{bytesPerFile:n1} bytes per file");
    }

    [TestMethod]
    public void LookupIsReleased() {
      var item = TextItem("a.cc", "int a;\n");
      Write(new[] { item });
      var cache = OpenNew();
      var contents = cache.Lookup.TryGetContents("a.cc", item.Metadata);
      cache.ReleaseLookup();
      Assert.IsNull(cache.Lookup);
      // The contents loaded before stay valid, and keep their location for the next write.
      Assert.AreEqual("int a;\n", GetText(contents));
      var result = cache.Write(Dirs(new[] { new Item("", "a.cc", item.Metadata, contents) }), VersionHash, LoadStart, CancellationToken.None);
      Assert.AreEqual(0, result.NewFileCount);
    }

    [TestMethod]
    public void MappedContentsSurviveGarbageCollections() {
      var item = TextItem("a.cc", "int a;\n");
      Write(new[] { item });
      var cache = OpenNew();
      var contents = cache.Lookup.TryGetContents("a.cc", item.Metadata);
      // Only the contents reference the view (not the cache, nor the accessor of the view).
      cache.Invalidate();
      Assert.IsNull(cache.Lookup);
      for (var i = 0; i < 3; i++) {
        GC.Collect();
        GC.WaitForPendingFinalizers();
      }
      Assert.AreEqual("int a;\n", GetText(contents));
    }

    [TestMethod]
    public void EntryIsUsedOnlyIfTimeAndSizeMatch() {
      var item = TextItem("a.cc", "int a;\n");
      Write(new[] { item });
      var lookup = OpenNew().Lookup;

      Assert.IsNotNull(lookup.TryGetContents("a.cc", item.Metadata));
      Assert.IsNull(lookup.TryGetContents("a.cc", new FileMetadata(OldTime.AddSeconds(1), item.Metadata.Length)), "Other time");
      Assert.IsNull(lookup.TryGetContents("a.cc", new FileMetadata(OldTime, item.Metadata.Length + 1)), "Other size");
      Assert.IsNull(lookup.TryGetContents("b.cc", item.Metadata), "Other path");
    }

    [TestMethod]
    public void LookupUsesTheCaseSensitivityOfTheFileSystem() {
      var item = TextItem("a.cc", "int a;\n");
      Write(new[] { item });
      var contents = OpenNew().Lookup.TryGetContents("A.CC", item.Metadata);
      var caseSensitive = PathComparerRegistry.Default.Comparison == StringComparison.Ordinal;
      Assert.AreEqual(caseSensitive, contents == null);
    }

    [TestMethod]
    public void FilesModifiedDuringTheLoadAreNotWritten() {
      var racy = TextItem("racy.cc", "int racy;\n", LoadStart - TimeSpan.FromSeconds(1));
      var future = TextItem("future.cc", "int future;\n", LoadStart + TimeSpan.FromSeconds(10));
      var old = TextItem("old.cc", "int old;\n");
      var result = Write(new[] { racy, future, old });
      Assert.AreEqual(2, result.RacyFileCount);
      Assert.AreEqual(1, result.EntryCount);

      var lookup = OpenNew().Lookup;
      Assert.IsNull(lookup.TryGetContents("racy.cc", racy.Metadata));
      Assert.IsNull(lookup.TryGetContents("future.cc", future.Metadata));
      Assert.IsNotNull(lookup.TryGetContents("old.cc", old.Metadata));
    }

    [TestMethod]
    public void FilesModifiedAfterTheScanAreNotWritten() {
      // The contents were read after the file was modified again (time of the scan != time of the contents).
      var contents = CreateText("int a;\n", OldTime.AddMinutes(1));
      var item = new Item("", "a.cc", new FileMetadata(OldTime, 7), contents);
      var result = Write(new[] { item });
      Assert.AreEqual(1, result.ChangedFileCount);
      Assert.AreEqual(0, result.EntryCount);
    }

    [TestMethod]
    public void FilesModifiedAfterTheScanAreDescribed() {
      // A real file: its metadata now, its attributes.
      Directory.CreateDirectory(Path.Combine(_projectRoot, "dir"));
      var path = Path.Combine(_projectRoot, "dir", "changed.cc");
      File.WriteAllText(path, "int changed;\n");
      try {
        var items = Enumerable.Range(0, 15).Select(i => new Item("dir", i == 0 ? "changed.cc" : $"other{i}.cc",
          new FileMetadata(OldTime, 13), CreateText("int changed;\n", OldTime.AddMinutes(1)))).ToArray();
        var result = Write(items);
        Assert.AreEqual(15, result.ChangedFileCount);
        Assert.AreEqual(10, result.ChangedFileSamples.Count, "At most 10 samples");
        var sample = result.ChangedFileSamples[0];
        StringAssert.Contains(sample, "\"" + Path.Combine("dir", "changed.cc") + "\"");
        StringAssert.Contains(sample, "13 bytes, contents " + OldTime.AddMinutes(1).ToString("O"));
        StringAssert.Contains(sample, "attributes");
      }
      finally {
        File.Delete(path);
      }
    }

    [TestMethod]
    public void LargeFilesAreNotWritten() {
      var text = new string('x', ProjectIndexCache.MaxCachedFileLength + 1);
      var result = Write(new[] { TextItem("large.txt", text), TextItem("small.txt", "small") });
      Assert.AreEqual(1, result.LargeFileCount);
      Assert.AreEqual(1, result.EntryCount);
    }

    [TestMethod]
    public void ContentsAlreadyWrittenAreNotWrittenAgain() {
      var items = new[] { TextItem("a.cc", "int a;\n"), TextItem("b.cc", "int b;\n") };
      var cache = OpenNew();
      Assert.AreEqual(2, cache.Write(Dirs(items), VersionHash, LoadStart, CancellationToken.None).NewFileCount);

      // Same contents in memory: same locations.
      var result = cache.Write(Dirs(items), VersionHash, LoadStart, CancellationToken.None);
      Assert.AreEqual(2, result.EntryCount);
      Assert.AreEqual(0, result.NewFileCount);
      Assert.AreEqual(0, result.NewArenaCount);

      // Contents loaded from the cache (mapped): same locations.
      cache.Dispose();
      var reopened = OpenNew();
      var loaded = items.Select(x => new Item(x.Directory, x.Name, x.Metadata,
        reopened.Lookup.TryGetContents(RelativePath(x), x.Metadata))).ToArray();
      result = reopened.Write(Dirs(loaded), VersionHash, LoadStart, CancellationToken.None);
      Assert.AreEqual(2, result.EntryCount);
      Assert.AreEqual(0, result.NewFileCount);
      Assert.AreEqual(1, ArenaFiles().Length);
    }

    [TestMethod]
    public void UnreferencedArenaFilesAreDeletedAfterAWrite() {
      var cache = OpenNew();
      cache.Write(Dirs(new[] { TextItem("a.cc", "int a;\n") }), VersionHash, LoadStart, CancellationToken.None);
      var first = ArenaFiles();
      Assert.AreEqual(1, first.Length);

      // a.cc is deleted, b.cc is new.
      var result = cache.Write(Dirs(new[] { TextItem("b.cc", "int b;\n") }), VersionHash, LoadStart, CancellationToken.None);
      Assert.AreEqual(1, result.DeletedArenaCount);
      var second = ArenaFiles();
      Assert.AreEqual(1, second.Length);
      Assert.AreNotEqual(first[0], second[0]);
    }

    [TestMethod]
    public void OrphanArenaFilesAreDeletedWhenOpened() {
      Write(new[] { TextItem("a.cc", "int a;\n") });
      var orphan = Path.Combine(ArenasDirectory, IndexCacheFile.GetArenaFileName(999));
      File.WriteAllBytes(orphan, new byte[16]);
      var cache = OpenNew();
      Assert.IsFalse(File.Exists(orphan));
      Assert.AreEqual(1, cache.Lookup.Count);
    }

    [TestMethod]
    public void SecondServerUsesTheCacheReadOnly() {
      var item = TextItem("a.cc", "int a;\n");
      Write(new[] { item });
      var indexTime = File.GetLastWriteTimeUtc(IndexPath);

      var first = OpenNew();
      Assert.IsTrue(first.IsWritable);
      var second = OpenNew();
      Assert.IsFalse(second.IsWritable);
      Assert.IsNotNull(second.Lookup.TryGetContents("a.cc", item.Metadata), "Read-only caches are used");

      Assert.IsNull(second.Write(Dirs(new[] { TextItem("b.cc", "int b;\n") }), VersionHash, LoadStart, CancellationToken.None));
      Assert.AreEqual(indexTime, File.GetLastWriteTimeUtc(IndexPath));
      Assert.AreEqual(1, ArenaFiles().Length);
    }

    [TestMethod]
    public void TruncatedIndexIsIgnoredAndDeleted() {
      Write(new[] { TextItem("a.cc", "int a;\n") });
      var bytes = File.ReadAllBytes(IndexPath);
      File.WriteAllBytes(IndexPath, bytes.Take(bytes.Length - 3).ToArray());
      AssertCacheIgnoredAndDeleted();
    }

    [TestMethod]
    public void CorruptIndexIsIgnoredAndDeleted() {
      Write(new[] { TextItem("a.cc", "int a;\n") });
      var bytes = File.ReadAllBytes(IndexPath);
      // The offset of the entry (after the path "a.cc", time, size and flags).
      // The bytes used of the arena (arena table before the trailer: 1 byte) are less than the
      // slot of the entry (16 bytes).
      Assert.AreEqual(16, bytes[bytes.Length - 9]);
      bytes[bytes.Length - 9] = 8;
      File.WriteAllBytes(IndexPath, bytes);
      AssertCacheIgnoredAndDeleted();
    }

    [TestMethod]
    public void IndexOfAnotherVersionIsIgnoredAndDeleted() {
      Write(new[] { TextItem("a.cc", "int a;\n") });
      var bytes = File.ReadAllBytes(IndexPath);
      bytes[4] = IndexCacheFile.FormatVersion + 1;
      File.WriteAllBytes(IndexPath, bytes);
      AssertCacheIgnoredAndDeleted();
    }

    [TestMethod]
    public void TruncatedArenaFileIsIgnoredAndDeleted() {
      Write(new[] { TextItem("a.cc", "int a;\n") });
      File.WriteAllBytes(ArenaFiles()[0], new byte[4]);
      AssertCacheIgnoredAndDeleted();
    }

    [TestMethod]
    public void InvalidateDeletesTheCache() {
      var item = TextItem("a.cc", "int a;\n");
      var cache = OpenNew();
      cache.Write(Dirs(new[] { item }), VersionHash, LoadStart, CancellationToken.None);
      cache.Invalidate();
      Assert.IsFalse(File.Exists(IndexPath));
      Assert.AreEqual(0, ArenaFiles().Length);

      // The contents in memory are written again by the next write.
      var result = cache.Write(Dirs(new[] { item }), VersionHash, LoadStart, CancellationToken.None);
      Assert.AreEqual(1, result.NewFileCount);
    }

    [TestMethod]
    public void ProjectDirectoryNameIsAHashOfTheRootPath() {
      var name = ProjectIndexCache.GetProjectDirectoryName(@"C:\src\project", caseSensitive: false);
      Assert.AreEqual(16, name.Length);
      Assert.AreEqual(name, ProjectIndexCache.GetProjectDirectoryName(@"c:\SRC\project\", caseSensitive: false));
      Assert.AreNotEqual(name, ProjectIndexCache.GetProjectDirectoryName(@"c:\SRC\project", caseSensitive: true));
      Assert.AreNotEqual(name, ProjectIndexCache.GetProjectDirectoryName(@"C:\src\project2", caseSensitive: false));
    }

    [TestMethod]
    public void OptionsFromTheCommandLine() {
      Assert.AreEqual(Path.GetFullPath("cachedir"),
        IndexCacheOptions.FromCommandLine(new[] { "--jsonrpc", "--cache-dir", "cachedir" }).CacheDirectory);
      Assert.IsFalse(IndexCacheOptions.FromCommandLine(new[] { "--jsonrpc", "--no-cache" }).IsEnabled);
    }

    [TestMethod]
    public void TreeRoundTrip() {
      var factory = new FileSystemNameFactory();
      var large = new string('x', ProjectIndexCache.MaxCachedFileLength + 1);
      var directories = new List<IndexCacheWriteDirectory> {
        new IndexCacheWriteDirectory("", false, new[] { ToWriteItem(TextItem("root.cc", "int root;")) }),
        new IndexCacheWriteDirectory("a", false, new[] {
          ToWriteItem(TextItem(Path.Combine("a", "large.txt"), large)),
          new IndexCacheWriteItem("not_searchable.txt", new FileMetadata(OldTime, 3), null),
          ToWriteItem(TextItem(Path.Combine("a", "racy.cc"), "int racy;", LoadStart)),
        }),
        new IndexCacheWriteDirectory(Path.Combine("a", "empty"), false, Array.Empty<IndexCacheWriteItem>()),
        new IndexCacheWriteDirectory(Path.Combine("a", "link"), true, new[] { ToWriteItem(BinaryItem(Path.Combine("a", "link", "b.bin"), 10)) }),
        new IndexCacheWriteDirectory("b", false, new[] { ToWriteItem(TextItem(Path.Combine("b", "b.cc"), "int b;")) }),
      };
      var cache = OpenNew();
      var result = cache.Write(directories, VersionHash, LoadStart, CancellationToken.None);
      Assert.AreEqual(6, result.TreeFileCount);
      Assert.AreEqual(5, result.TreeDirectoryCount);
      Assert.AreEqual(3, result.EntryCount, "root.cc, b.bin, b.cc");
      cache.Dispose();

      var rootPath = new FullPath(_projectRoot);
      var tree = OpenNew().TryLoadTree(VersionHash, factory, rootPath, CancellationToken.None);
      Assert.IsNotNull(tree);
      var root = tree.Root;
      Assert.AreEqual(rootPath, root.DirectoryName.FullPath);
      CollectionAssert.AreEqual(new[] { "root.cc" }, root.ChildFiles.Select(x => x.Name).ToArray());
      CollectionAssert.AreEqual(new[] { "a", "b" }, root.ChildDirectories.Select(x => x.DirectoryName.Name).ToArray());
      var a = root.ChildDirectories[0];
      CollectionAssert.AreEqual(new[] { "large.txt", "not_searchable.txt", "racy.cc" }, a.ChildFiles.Select(x => x.Name).ToArray());
      Assert.AreEqual(new FileMetadata(OldTime, 3), a.ChildFileMetadata[1]);
      CollectionAssert.AreEqual(new[] { "empty", "link" }, a.ChildDirectories.Select(x => x.DirectoryName.Name).ToArray());
      Assert.IsFalse(a.ChildDirectories[0].IsSymLink);
      Assert.IsTrue(a.ChildDirectories[1].IsSymLink);
      Assert.AreEqual(Path.Combine(_projectRoot, "a", "link", "b.bin"), a.ChildDirectories[1].ChildFiles[0].FullPath.Value);
      // The searchable files without cached contents are read by the verification of the tree.
      CollectionAssert.AreEquivalent(new[] { "large.txt", "racy.cc" }, tree.FilesWithoutContents.Select(x => x.Name).ToArray());
    }

    [TestMethod]
    public void TreeIsNotUsedIfTheConfigurationChanged() {
      Write(new[] { TextItem("a.cc", "int a;") });
      var cache = OpenNew();
      Assert.IsNull(cache.TryLoadTree("other hash", new FileSystemNameFactory(), new FullPath(_projectRoot), CancellationToken.None));
      // The contents are still used (phase 1).
      Assert.IsNotNull(cache.Lookup);
    }

    [TestMethod]
    public void TreeWithADirectoryWithoutParentIsIgnoredAndDeleted() {
      Write(new[] { TextItem("a.cc", "int a;") });
      using (var stream = new FileStream(IndexPath, FileMode.Create)) {
        using (var writer = new IndexCacheFileWriter(stream, _projectRoot, VersionHash, PathComparerRegistry.Default.Comparison == StringComparison.Ordinal)) {
          writer.AddDirectory("", IndexCacheDirectoryFlags.None);
          writer.AddDirectory(Path.Combine("missing", "child"), IndexCacheDirectoryFlags.None);
          writer.Finish(new Dictionary<int, int>());
        }
      }
      AssertCacheIgnoredAndDeleted();
    }

    private void AssertCacheIgnoredAndDeleted() {
      var cache = OpenNew();
      Assert.IsNull(cache.Lookup);
      Assert.IsFalse(File.Exists(IndexPath));
      Assert.AreEqual(0, ArenaFiles().Length);
    }

    private static int IndexOf(byte[] bytes, byte[] value) {
      for (var i = 0; i + value.Length <= bytes.Length; i++) {
        if (bytes.AsSpan(i, value.Length).SequenceEqual(value))
          return i;
      }
      throw new InvalidOperationException("Not found");
    }

    private string CacheDirectory => Path.Combine(_cacheRoot,
      ProjectIndexCache.GetProjectDirectoryName(_projectRoot, PathComparerRegistry.Default.Comparison == StringComparison.Ordinal));
    private string IndexPath => Path.Combine(CacheDirectory, IndexCacheFile.IndexFileName);
    private string ArenasDirectory => Path.Combine(CacheDirectory, IndexCacheFile.ArenasDirectoryName);
    private string[] ArenaFiles() => Directory.GetFiles(ArenasDirectory, "*.arena");

    private ProjectIndexCache OpenNew() {
      var cache = new ProjectIndexCache(_cacheRoot, _projectRoot);
      _caches.Add(cache);
      cache.Open();
      return cache;
    }

    /// <summary>Writes the items with a new cache (released after the write).</summary>
    private IndexCacheWriteResult Write(IEnumerable<Item> items) {
      var cache = OpenNew();
      var result = cache.Write(Dirs(items), VersionHash, LoadStart, CancellationToken.None);
      cache.Dispose();
      return result;
    }

    private const string VersionHash = "version hash";

    /// <summary>A file of a directory (tests).</summary>
    private sealed record Item(string Directory, string Name, FileMetadata Metadata, FileContents Contents);

    private static IndexCacheWriteItem ToWriteItem(Item item) => new IndexCacheWriteItem(item.Name, item.Metadata, item.Contents);

    /// <summary>
    /// The directories of <paramref name="items"/> and their ancestors, in depth first order.
    /// </summary>
    private static IEnumerable<IndexCacheWriteDirectory> Dirs(IEnumerable<Item> items) {
      var byDirectory = new Dictionary<string, List<IndexCacheWriteItem>>(StringComparer.OrdinalIgnoreCase) { [""] = new List<IndexCacheWriteItem>() };
      foreach (var item in items) {
        for (var d = item.Directory; d.Length > 0; d = Path.GetDirectoryName(d) ?? "") {
          if (!byDirectory.ContainsKey(d))
            byDirectory[d] = new List<IndexCacheWriteItem>();
        }
        byDirectory[item.Directory].Add(ToWriteItem(item));
      }
      var sorted = byDirectory.Keys.OrderBy(x => x.Split(Path.DirectorySeparatorChar), Comparer<string[]>.Create((x, y) => {
        for (var i = 0; i < Math.Min(x.Length, y.Length); i++) {
          var c = string.Compare(x[i], y[i], StringComparison.OrdinalIgnoreCase);
          if (c != 0)
            return c;
        }
        return x.Length.CompareTo(y.Length);
      })).Select(x => x.Length == 0 ? x : x).ToList();
      // "" splits into [""]: first.
      return sorted.Select(x => new IndexCacheWriteDirectory(x, false, byDirectory[x]));
    }

    private static string RelativePath(Item item) {
      return item.Directory.Length == 0 ? item.Name : Path.Combine(item.Directory, item.Name);
    }

    private static (string Directory, string Name) Split(string relativePath) {
      return (Path.GetDirectoryName(relativePath) ?? "", Path.GetFileName(relativePath));
    }

    private Item TextItem(string relativePath, string text, DateTime? time = null) {
      var lastWriteTime = time ?? OldTime;
      var (directory, name) = Split(relativePath);
      return new Item(directory, name, new FileMetadata(lastWriteTime, Encoding.UTF8.GetByteCount(text)),
        CreateText(text, lastWriteTime));
    }

    private static Item BinaryItem(string relativePath, long length) {
      var (directory, name) = Split(relativePath);
      return new Item(directory, name, new FileMetadata(OldTime, length), new BinaryFileContents(OldTime, length));
    }

    private FileContents CreateText(string text, DateTime lastWriteTime) {
      var bytes = Encoding.UTF8.GetBytes(text);
      var slice = _arenas.Allocate(bytes.Length + 2);
      bytes.CopyTo(slice.Span);
      slice.Span.Slice(bytes.Length).Clear();
      return new Utf8FileContents(new FileContentsMemory(slice.Block, slice.Offset, bytes.Length), lastWriteTime,
        isAscii: System.Text.Ascii.IsValid(bytes));
    }

    private static unsafe string GetText(FileContents contents) {
      var bytes = new ReadOnlySpan<byte>(contents.Contents.Pointer.ToPointer(), contents.ByteLength + 2);
      Assert.AreEqual(0, bytes[contents.ByteLength], "Trailing NUL bytes");
      Assert.AreEqual(0, bytes[contents.ByteLength + 1], "Trailing NUL bytes");
      return Encoding.UTF8.GetString(bytes.Slice(0, contents.ByteLength));
    }
  }
}
