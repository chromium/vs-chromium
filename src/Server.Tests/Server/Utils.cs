// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.FileSystemContents;

namespace VsChromium.Tests.Server {
  public static class Utils {
    public static DirectoryInfo GetTestDataDirectory() {
      var assemblyFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
      var testDataPath = Path.Combine(GetRepositoryRoot(assemblyFileInfo.Directory).FullName, "src", "Tests", "TestData");
      var result = new DirectoryInfo(testDataPath);
      Assert.IsTrue(result.Exists, string.Format("Test data path \"{0}\" not found!", testDataPath));
      return result;
    }

    /// <summary>
    /// The repository root is the first parent directory containing the "build.root" marker file.
    /// </summary>
    private static DirectoryInfo GetRepositoryRoot(DirectoryInfo start) {
      for (var dir = start; dir != null; dir = dir.Parent) {
        if (File.Exists(Path.Combine(dir.FullName, "build.root"))) {
          return dir;
        }
      }
      throw new InvalidOperationException($"Repository root (build.root) not found above \"{start.FullName}\"");
    }

    public static DirectoryInfo GetChromiumTestEnlistmentDirectory() {
      var baseInfo = GetTestDataDirectory();
      var result = new DirectoryInfo(Path.Combine(baseInfo.FullName, "chromium", "src"));
      Assert.IsTrue(result.Exists, string.Format("Chromium test enlistment \"{0}\" not found!", result));
      return result;
    }


    public static FileInfo GetChromiumTestEnlistmentFile() {
#if REAL_ENLISTMENT_TEST
      var filePath = @"D:\src\chromium\head\src\PRESUBMIT.py";
#else
      var filePath = Path.Combine(GetChromiumTestEnlistmentDirectory().FullName, "PRESUBMIT.py");
      var result = new FileInfo(filePath);
      Assert.IsTrue(result.Exists, string.Format("Test data file \"{0}\" not found!", filePath));
      return result;
#endif
    }

    public static FileContentsMemory CreateUtf8Memory(byte[] bytes) {
      var block = HeapAllocStatic.Alloc(bytes.Length + 1);
      Marshal.Copy(bytes, 0, block.Pointer, bytes.Length);
      Marshal.WriteByte(block.Pointer, bytes.Length, 0);
      return new FileContentsMemory(block, 0, bytes.Length);
    }

    /// <summary>
    /// File contents of the UTF-8 encoding of <paramref name="text"/>.
    /// </summary>
    public static Utf8FileContents CreateUtf8FileContents(string text) {
      return CreateUtf8FileContents(Encoding.UTF8.GetBytes(text));
    }

    public static Utf8FileContents CreateUtf8FileContents(byte[] bytes) {
      return new Utf8FileContents(CreateUtf8Memory(bytes), DateTime.Now, isAscii: System.Text.Ascii.IsValid(bytes));
    }

    /// <summary>
    /// File contents of the UTF-8 encoding of <paramref name="text"/>, using the code paths of
    /// text with non ASCII characters even if <paramref name="text"/> is ASCII only.
    /// </summary>
    public static Utf8FileContents CreateNonAsciiFileContents(string text) {
      return new Utf8FileContents(CreateUtf8Memory(Encoding.UTF8.GetBytes(text)), DateTime.Now, isAscii: false);
    }
  }
}
