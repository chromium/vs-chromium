// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Win32.Strings;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestConversion {
    [TestMethod]
    public void ConcurrentBitArrayWorks() {
      var test = "/css";
      var mem = Conversion.StringToUtf8(test);
      var bytes = mem.ToArray();
      Assert.AreEqual('/', (char)bytes[0]);
      Assert.AreEqual('c', (char)bytes[1]);
      Assert.AreEqual('s', (char)bytes[2]);
      Assert.AreEqual('s', (char)bytes[3]);
    }
  }
}
