// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestPlatformPathHelpers {
    private static readonly IPlatformPathHelper Windows = new WindowsPathHelper();
    private static readonly IPlatformPathHelper Linux = new LinuxPathHelper();
    private static readonly IPlatformPathHelper Mac = new MacPathHelper();

    [TestMethod]
    public void PlatformSingletonMatchesCurrentOperatingSystem() {
      var first = PathHelpers.Platform;
      var second = PathHelpers.Platform;
      Assert.AreSame(first, second);

      if (OperatingSystem.IsWindows()) {
        Assert.IsInstanceOfType<WindowsPathHelper>(first);
      } else if (OperatingSystem.IsLinux()) {
        Assert.IsInstanceOfType<LinuxPathHelper>(first);
      } else if (OperatingSystem.IsMacOS()) {
        Assert.IsInstanceOfType<MacPathHelper>(first);
      }
    }

    [TestMethod]
    public void SeparatorsAndCaseSensitivityPerPlatform() {
      Assert.AreEqual('\\', Windows.DirectorySeparatorChar);
      Assert.AreEqual(StringComparison.OrdinalIgnoreCase, Windows.FileNameComparison);
      Assert.IsTrue(Windows.FileNameComparer.Equals("Foo.cc", "foo.CC"));

      Assert.AreEqual('/', Linux.DirectorySeparatorChar);
      Assert.AreEqual(StringComparison.Ordinal, Linux.FileNameComparison);
      Assert.IsFalse(Linux.FileNameComparer.Equals("Foo.cc", "foo.CC"));
      Assert.IsTrue(Linux.FileNameComparer.Equals("foo.cc", "foo.cc"));

      Assert.AreEqual('/', Mac.DirectorySeparatorChar);
      Assert.AreEqual(StringComparison.OrdinalIgnoreCase, Mac.FileNameComparison);
      Assert.IsTrue(Mac.FileNameComparer.Equals("Foo.cc", "foo.CC"));
    }

    [TestMethod]
    public void IsAbsolutePathPerPlatform() {
      Assert.IsTrue(Windows.IsAbsolutePath(@"c:\foo"));
      Assert.IsTrue(Windows.IsAbsolutePath("c:/foo"));
      Assert.IsTrue(Windows.IsAbsolutePath(@"\\server\share"));
      Assert.IsFalse(Windows.IsAbsolutePath("/foo/bar"));
      Assert.IsFalse(Windows.IsAbsolutePath(@"foo\bar"));
      Assert.IsFalse(Windows.IsAbsolutePath("c:foo"));

      foreach (var posix in new[] { Linux, Mac }) {
        Assert.IsTrue(posix.IsAbsolutePath("/"));
        Assert.IsTrue(posix.IsAbsolutePath("/foo/bar"));
        Assert.IsFalse(posix.IsAbsolutePath(@"c:\foo"));
        Assert.IsFalse(posix.IsAbsolutePath(@"\\server\share"));
        Assert.IsFalse(posix.IsAbsolutePath("foo/bar"));
        Assert.IsFalse(posix.IsAbsolutePath("a:b"));
      }
    }

    [TestMethod]
    public void GetParentPerPlatform() {
      Assert.AreEqual(@"c:\foo\bar", Windows.GetParent(@"c:\foo\bar\baz.txt"));
      Assert.AreEqual(@"c:\foo", Windows.GetParent(@"c:\foo\bar"));
      Assert.AreEqual(@"c:\foo", Windows.GetParent(@"c:\foo\bar\"));
      Assert.AreEqual(@"c:\", Windows.GetParent(@"c:\foo"));
      Assert.AreEqual(@"c:\", Windows.GetParent(@"c:\foo\"));
      Assert.IsNull(Windows.GetParent(@"c:\"));

      Assert.AreEqual(@"\\server\share\dir", Windows.GetParent(@"\\server\share\dir\file.cc"));
      Assert.AreEqual(@"\\server\share", Windows.GetParent(@"\\server\share\dir"));
      Assert.IsNull(Windows.GetParent(@"\\server\share"));
      Assert.IsNull(Windows.GetParent(@"\\server\share\"));
      Assert.IsNull(Windows.GetParent(@"\\server"));
      Assert.IsNull(Windows.GetParent(@"\\"));

      Assert.AreEqual(@"foo\bar", Windows.GetParent(@"foo\bar\baz"));
      Assert.AreEqual("foo", Windows.GetParent(@"foo\bar"));
      Assert.IsNull(Windows.GetParent("foo"));

      foreach (var posix in new[] { Linux, Mac }) {
        Assert.AreEqual("/foo/bar", posix.GetParent("/foo/bar/baz.txt"));
        Assert.AreEqual("/foo", posix.GetParent("/foo/bar"));
        Assert.AreEqual("/foo", posix.GetParent("/foo/bar/"));
        Assert.AreEqual("/", posix.GetParent("/foo"));
        Assert.AreEqual("/", posix.GetParent("/foo/"));
        Assert.IsNull(posix.GetParent("/"));
        Assert.IsNull(posix.GetParent("///"));

        Assert.AreEqual("foo/bar", posix.GetParent("foo/bar/baz"));
        Assert.AreEqual("foo", posix.GetParent("foo/bar"));
        Assert.IsNull(posix.GetParent("foo"));
        Assert.IsNull(posix.GetParent(@"foo\bar"));
      }
    }

    [TestMethod]
    public void FileNameAndExtensionPerPlatform() {
      Assert.IsTrue(Windows.IsFileName("foo.cc"));
      Assert.IsFalse(Windows.IsFileName(@"dir\foo.cc"));
      Assert.IsFalse(Windows.IsFileName("dir/foo.cc"));
      Assert.AreEqual("bar.cc", Windows.GetFileName(@"c:\foo\bar.cc"));
      Assert.AreEqual(".cc", Windows.GetExtension(@"c:\foo.dir\bar.cc"));
      Assert.AreEqual("", Windows.GetExtension(@"c:\foo.dir\bar"));

      foreach (var posix in new[] { Linux, Mac }) {
        Assert.IsTrue(posix.IsFileName("foo.cc"));
        Assert.IsTrue(posix.IsFileName(@"foo\bar.cc"));
        Assert.IsTrue(posix.IsFileName("a:b.cc"));
        Assert.IsFalse(posix.IsFileName("dir/foo.cc"));
        Assert.AreEqual(@"foo\bar.cc", posix.GetFileName(@"/dir/foo\bar.cc"));
        Assert.AreEqual("a:b.cc", posix.GetFileName("/dir/a:b.cc"));
        Assert.AreEqual(".cc", posix.GetExtension("/dir.old/a:b.cc"));
        Assert.AreEqual("", posix.GetExtension(@"/dir.old/file\noext"));
      }
    }

    [TestMethod]
    public void IsPrefixSplitAndCombinePerPlatform() {
      Assert.IsTrue(Windows.IsPrefix(@"c:\Foo", @"C:\foo\bar.cc"));
      Assert.IsFalse(Windows.IsPrefix("foo", @"bar\foo"));
      CollectionAssert.AreEqual(new[] { "c:", "foo", "bar.cc" }, Windows.SplitPath(@"c:\foo\bar.cc").ToArray());
      CollectionAssert.AreEqual(new[] { @"\\server", "share", "foo" }, Windows.SplitPath(@"\\server\share\foo").ToArray());

      Assert.IsTrue(Linux.IsPrefix("/foo", "/foo/bar.cc"));
      Assert.IsFalse(Linux.IsPrefix("/Foo", "/foo/bar.cc"));
      Assert.IsTrue(Linux.IsPrefix("/", "/foo/bar.cc"));
      Assert.IsTrue(Mac.IsPrefix("/Foo", "/foo/bar.cc"));

      var unixRootSplit = Linux.SplitPrefix("/ooo/bar.txt", "/");
      Assert.AreEqual("/", unixRootSplit.Root);
      Assert.AreEqual("ooo/bar.txt", unixRootSplit.Suffix);

      var unixDirSplit = Mac.SplitPrefix("/Users/dev/proj/src/a.cc", "/Users/dev/proj/");
      Assert.AreEqual("/Users/dev/proj", unixDirSplit.Root);
      Assert.AreEqual("src/a.cc", unixDirSplit.Suffix);

      foreach (var posix in new[] { Linux, Mac }) {
        CollectionAssert.AreEqual(new[] { "usr", "local", "bin" }, posix.SplitPath("/usr/local/bin").ToArray());
        CollectionAssert.AreEqual(new[] { @"a\b", "c:d" }, posix.SplitPath(@"a\b/c:d").ToArray());
        Assert.AreEqual("/usr/bin", posix.CombinePaths("/usr", "bin"));
        Assert.AreEqual("/bin", posix.CombinePaths("/", "bin"));
        Assert.AreEqual("/dir:1/file.cc", posix.CombinePaths("/dir:1", "file.cc"));
      }
    }

    [TestMethod]
    public void ValidBclPathPerPlatform() {
      Assert.IsTrue(Windows.IsValidBclPath(@"c:\foo\bar.cc"));
      Assert.IsFalse(Windows.IsValidBclPath("c:\\foo\\bad|name.cc"));
      Assert.IsFalse(Windows.IsValidBclPath("c:\\foo\\bad\0name.cc"));

      foreach (var posix in new[] { Linux, Mac }) {
        Assert.IsTrue(posix.IsValidBclPath("/foo/pipe|allowed<here>.cc"));
        Assert.IsFalse(posix.IsValidBclPath("/foo/bad\0name.cc"));
      }
    }
  }
}
