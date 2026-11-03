// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;
using VsChromium.Server.FileSystemContents;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileContentsFactory {
    /// <summary>
    /// Regression test: the native memory of a file whose contents failed to be processed (after
    /// reading) was only released by the finalizer of its handle (up to 2 GB per file).
    /// </summary>
    [TestMethod]
    public void NativeMemoryIsReleasedWhenProcessingTheContentsFails() {
      var fileSystem = new InvalidBlockFileSystem();
      var contents = new FileContentsFactory(fileSystem).ReadFileContents(new FullPath(@"C:\foo.txt"));

      Assert.AreSame(BinaryFileContents.Empty, contents);
      Assert.IsNotNull(fileSystem.LastBlock);
      Assert.IsTrue(fileSystem.LastBlock.IsClosed, "The native memory block was not released");
    }

    /// <summary>
    /// Returns a block smaller than the requested trailing bytes, so that processing the contents
    /// fails.
    /// </summary>
    private class InvalidBlockFileSystem : PortableFileSystem, IFileContentsReader {
      public SafeNativeMemoryHandle LastBlock { get; private set; }

      public new SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, int trailingByteCount, out DateTime lastWriteTimeUtc) {
        lastWriteTimeUtc = DateTime.UtcNow;
        LastBlock = NativeHeap.Alloc(Math.Max(0, trailingByteCount - 1));
        return LastBlock;
      }
    }
  }
}
