// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VsChromium.Tests.Server {
  /// <summary>
  /// Location of the test data used by the server process tests (the server unit tests, and their own
  /// copy of these helpers, are in src/Server.Tests).
  /// </summary>
  public static class Utils {
    public static DirectoryInfo GetTestDataDirectory() {
      var assemblyFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
      var testDataPath = Path.Combine(assemblyFileInfo.Directory.Parent.Parent.FullName, "src", "Tests", "TestData");
      var result = new DirectoryInfo(testDataPath);
      Assert.IsTrue(result.Exists, string.Format("Test data path \"{0}\" not found!", testDataPath));
      return result;
    }

    public static DirectoryInfo GetChromiumTestEnlistmentDirectory() {
      var baseInfo = GetTestDataDirectory();
      var result = new DirectoryInfo(Path.Combine(baseInfo.FullName, "chromium", "src"));
      Assert.IsTrue(result.Exists, string.Format("Chromium test enlistment \"{0}\" not found!", result));
      return result;
    }

    public static FileInfo GetChromiumTestEnlistmentFile() {
      var filePath = Path.Combine(GetChromiumTestEnlistmentDirectory().FullName, "PRESUBMIT.py");
      var result = new FileInfo(filePath);
      Assert.IsTrue(result.Exists, string.Format("Test data file \"{0}\" not found!", filePath));
      return result;
    }
  }
}
