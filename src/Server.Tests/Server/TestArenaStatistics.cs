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
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.JsonRpc;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestArenaStatistics {
    /// <summary>
    /// Regression test: the live bytes were counted from the pieces of the database, computed when
    /// the snapshot is created. During a load, the current database is an intermediate snapshot
    /// sharing the contents array of the load in progress: the contents loaded since it was
    /// created were counted as dead (hundreds of MB on large trees).
    /// </summary>
    [TestMethod]
    public void ContentsLoadedAfterAnIntermediateSnapshotAreLive() {
      var arenas = new ArenaAllocator();
      var factory = new FileSystemNameFactory();
      var root = factory.CreateAbsoluteDirectoryName(new FullPath(Path.Combine(Path.GetTempPath(), "root")));
      var names = new[] { "a.cc", "b.cc", "c.cc", "empty.txt" }.Select(x => factory.CreateFileName(root, x)).ToArray();
      var contents = new FileContents[names.Length];
      contents[0] = CreateContents(arenas, "text of a");
      // Intermediate snapshot: only a.cc is loaded.
      var database = new FileDatabaseSnapshot(new Dictionary<FullPath, string>(),
        new Dictionary<DirectoryName, FileRange> { [root] = new FileRange(0, names.Length) },
        names, contents);
      database.PrecomputeSearchData();

      // The load continues (same contents array).
      contents[1] = CreateContents(arenas, "text of b");
      contents[2] = CreateContents(arenas, "text of c");
      // Empty files have a slot (trailing bytes), but no piece.
      contents[3] = CreateContents(arenas, "");

      var statistics = ProtocolRequestHandler.GetArenaStatistics(arenas, database);
      Assert.AreEqual(16 + 16 + 16 + 8, statistics.UsedBytes);
      Assert.AreEqual(0, statistics.DeadBytes);
    }

    private static FileContents CreateContents(ArenaAllocator arenas, string text) {
      var bytes = Encoding.UTF8.GetBytes(text);
      var slice = arenas.Allocate(bytes.Length + 2);
      bytes.CopyTo(slice.Span);
      slice.Span.Slice(bytes.Length).Clear();
      return new Utf8FileContents(new FileContentsMemory(slice.Block, slice.Offset, bytes.Length), DateTime.UtcNow, isAscii: true);
    }
  }
}
