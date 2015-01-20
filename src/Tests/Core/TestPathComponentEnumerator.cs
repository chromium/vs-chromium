// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestPathComponentEnumerator {
    [TestMethod]
    public void PathComponentEnumeratorWorks() {
      Test(null, new string[] { });
      Test(@"", new string[] { });
      Test(@"a", new string[] { "a" });
      Test(@"a\b", new string[] { "a", "b" });
      Test(@"a1\b2\c3", new string[] { "a1", "b2", "c3" });
      Test(@"a1/b2/c3", new string[] { "a1", "b2", "c3" });
    }

    private void Test(string path, IEnumerable<string> values) {
      Assert.IsTrue(new PathComponentSplitter(path, PathComparisonOption.CaseInsensitive)
        .Select(x => x.ToString())
        .SequenceEqual(values));
    }
  }
}
