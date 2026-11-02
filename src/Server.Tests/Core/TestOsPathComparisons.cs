// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestOsPathComparisons {
    [TestMethod]
    public void DefaultPathComparerMatchesOperatingSystem() {
      // Case-sensitive on Linux, case-insensitive on Windows and macOS.
      var caseSensitive = OperatingSystem.IsLinux();
      Assert.AreEqual(!caseSensitive, PathComparerRegistry.Default.StringComparer.Equals("foo.TXT", "FOO.txt"));
      Assert.AreEqual(!caseSensitive, SystemPathComparer.EqualsNames("foo.TXT", "FOO.txt"));
      Assert.IsTrue(SystemPathComparer.EqualsNames("foo.txt", "foo.txt"));
    }

    [TestMethod]
    public void AbsolutePathsMatchOperatingSystem() {
      if (OperatingSystem.IsWindows()) {
        Assert.IsTrue(PathHelpers.IsAbsolutePath(@"c:\foo"));
        Assert.IsTrue(PathHelpers.IsAbsolutePath(@"\\server\share"));
        Assert.IsFalse(PathHelpers.IsAbsolutePath(@"foo\bar"));
      } else {
        Assert.IsTrue(PathHelpers.IsAbsolutePath("/foo/bar"));
        Assert.IsFalse(PathHelpers.IsAbsolutePath("foo/bar"));
        Assert.IsFalse(PathHelpers.IsAbsolutePath("a:b"));
      }
    }
  }
}
