// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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

    public static FileContentsMemory CreateAsciiMemory(string text) {
      var size = text.Length + 1;
      var handle = new SafeHGlobalHandle(Marshal.StringToHGlobalAnsi(text));
      return new FileContentsMemory(handle, size, 0, size - 1);
    }

    public static FileContentsMemory CreateUtf16Memory(string text) {
      var size = (text.Length + 1) * sizeof(char);
      var handle = new SafeHGlobalHandle(Marshal.StringToHGlobalUni(text));
      return new FileContentsMemory(handle, size, 0, size - 1);
    }

    public static AsciiFileContents CreateAsciiFileContents(string text) {
      var memory = CreateAsciiMemory(text);
      var contents = new AsciiFileContents(memory, DateTime.Now);
      return contents;
    }

    public static Utf16FileContents CreateUtf16FileContents(string text) {
      var memory = CreateUtf16Memory(text);
      var contents = new Utf16FileContents(memory, DateTime.Now);
      return contents;
    }
  }
}
