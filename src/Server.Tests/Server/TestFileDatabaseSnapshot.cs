// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemDatabase.Builder;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Tests.Server {
  /// <summary>
  /// The compact layout of the file database: file names and contents in parallel arrays, files
  /// found by directory and binary search, pieces referring to the files by index.
  /// </summary>
  [TestClass]
  public class TestFileDatabaseSnapshot {
    private static readonly FullPath Root = new FullPath(Path.Combine(Path.GetTempPath(), "root"));

    [TestMethod]
    public void FilesAreFoundByDirectoryAndName() {
      var (database, factory, root) = CreateDatabase();
      var src = factory.CreateDirectoryName(root, "src");

      // Names created separately (e.g. from a path of a request): found by value.
      Assert.IsTrue(database.TryGetFileIndex(factory.CreateFileName(src, "b.cc"), out var index));
      Assert.AreEqual("b.cc", database.FileNames[index].Name);
      Assert.AreEqual("text of b", Text(database.GetContents(index)));
      Assert.IsTrue(database.TryGetFileIndex(factory.CreateFileName(root, "README"), out _));
      // Case-insensitive names on Windows and macOS (same as the file system).
      Assert.AreEqual(SystemPathComparer.EqualsNames("a", "A"), database.TryGetFileIndex(factory.CreateFileName(src, "B.CC"), out _));

      Assert.IsFalse(database.TryGetFileIndex(factory.CreateFileName(src, "missing.cc"), out _));
      Assert.IsFalse(database.TryGetFileIndex(factory.CreateFileName(factory.CreateDirectoryName(root, "other"), "a.cc"), out _));
      Assert.IsFalse(FileDatabaseSnapshot.Empty().TryGetFileIndex(factory.CreateFileName(src, "a.cc"), out _));
    }

    [TestMethod]
    public void PiecesAreSmallFilesAndChunksOfLargeFiles() {
      var (database, _, _) = CreateDatabase();
      var pieces = database.FileContentsPieces;

      // a.cc, b.cc, README (small) + the chunks of large.txt; the empty file has no piece.
      var large = database.GetContents(IndexOf(database, "large.txt"));
      var chunkCount = large.GetChunks().Count;
      Assert.IsTrue(chunkCount > 1);
      Assert.AreEqual(3 + chunkCount, pieces.Count);
      Assert.AreEqual(chunkCount, pieces.Count(x => x.FileName.Name == "large.txt"));
      Assert.AreEqual(1, pieces.Count(x => x.FileName.Name == "large.txt" && x.IsFirstPiece));
      Assert.AreEqual(large.ByteLength, pieces.Where(x => x.FileName.Name == "large.txt").Sum(x => x.ByteLength));
      Assert.AreEqual(4, database.SearchableFileCount);
      Assert.AreEqual(5, database.FileNameCount);
      // Pieces of the same file have the same file id (its index), used to group the results.
      Assert.AreEqual(1, pieces.Where(x => x.FileName.Name == "large.txt").Select(x => x.FileId).Distinct().Count());
    }

    [TestMethod]
    public void EditedSmallFilesAreSeenByTheExistingPieces() {
      var (database, _, _) = CreateDatabase();
      var pieces = database.FilePieces;
      var index = IndexOf(database, "a.cc");

      database.DangerousSetContents(index, CreateContents("new text of a"));

      var piece = pieces.Single(x => x.FileName.Name == "a.cc");
      Assert.AreEqual("new text of a", Text(piece.FileContents));
      Assert.AreEqual("new text of a".Length, piece.ByteLength);
    }

    [TestMethod]
    public void SmallFileBecomingLargeIsSearchedWithoutTrigramFilters() {
      var (database, _, _) = CreateDatabase();
      var pieces = database.FilePieces;
      var index = IndexOf(database, "b.cc");

      var largeText = new string('x', FileContents.MaxChunkLength * 2);
      database.DangerousSetContents(index, CreateContents(largeText));

      // Until new pieces are created: the whole text, without filters (chunk -1).
      var piece = pieces.Single(x => x.FileName.Name == "b.cc");
      Assert.AreEqual(largeText.Length, piece.ByteLength);
      Assert.IsTrue(piece.IsFirstPiece);
    }

    private static int IndexOf(FileDatabaseSnapshot database, string name) {
      for (var i = 0; i < database.FileNames.Count; i++) {
        if (database.FileNames[i].Name == name)
          return i;
      }
      throw new InvalidOperationException(name);
    }

    private static (FileDatabaseSnapshot, FileSystemNameFactory, DirectoryName) CreateDatabase() {
      var factory = new FileSystemNameFactory();
      var root = factory.CreateAbsoluteDirectoryName(Root);
      var src = factory.CreateDirectoryName(root, "src");
      var largeText = string.Join("\n", Enumerable.Range(0, 30_000).Select(i => "line " + i));
      // Grouped by directory, sorted by name in each directory.
      var files = new List<(FileName Name, FileContents Contents)> {
        (factory.CreateFileName(root, "empty.txt"), CreateContents("")),
        (factory.CreateFileName(root, "large.txt"), CreateContents(largeText)),
        (factory.CreateFileName(root, "README"), CreateContents("readme")),
        (factory.CreateFileName(src, "a.cc"), CreateContents("text of a")),
        (factory.CreateFileName(src, "b.cc"), CreateContents("text of b")),
      };
      var directories = new Dictionary<DirectoryName, FileRange> {
        [root] = new FileRange(0, 3),
        [src] = new FileRange(3, 2),
      };
      var database = new FileDatabaseSnapshot(new Dictionary<FullPath, string>(), directories,
        files.Select(x => x.Name).ToArray(), files.Select(x => x.Contents).ToArray());
      database.PrecomputeSearchData();
      return (database, factory, root);
    }

    private static unsafe FileContents CreateContents(string text) {
      var bytes = Encoding.UTF8.GetBytes(text);
      var block = NativeHeap.Alloc(bytes.Length + 2);
      var span = new Span<byte>(block.Pointer.ToPointer(), bytes.Length + 2);
      bytes.CopyTo(span);
      span.Slice(bytes.Length).Clear();
      return new Utf8FileContents(new FileContentsMemory(block, 0, bytes.Length), DateTime.UtcNow, isAscii: true);
    }

    private static unsafe string Text(FileContents contents) {
      var extract = contents.GetFileExtracts(1000, new[] { new VsChromium.Server.Search.FilePositionSpan { Position = 0, Length = 0 } }).Single();
      return extract.Text;
    }
  }
}
