// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using VsChromium.Core.Collections;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestSlimHashTable {
    [TestMethod]
    public void TestAddRemove() {
      var table = new SlimHashTable<string, string>(new Parameters(), 10, 3.0);
      table.Add("foo");
      AssertTableContents(table);
      var count = 32 * 5;
      for (var i = 0; i < count; i++) {
        table.Add("foo" + i);
        AssertTableContents(table);
      }
      AssertTableContents(table);
      Assert.IsTrue(table.Contains("foo"));
      Assert.AreEqual(count + 1, table.Count);
      Assert.IsFalse(table.Contains("bar"));

      AssertTableContents(table);
      for (var i = count - 1; i >= 0; i--) {
        var removed = table.Remove("foo" + i);
        Assert.IsTrue(removed);
        AssertTableContents(table);
      }
      Assert.IsTrue(table.Contains("foo"));
      Assert.AreEqual(1, table.Count);
      Assert.IsFalse(table.Contains("bar"));

      Assert.IsTrue(table.Remove("foo"));
      Assert.IsFalse(table.Contains("foo"));
      Assert.AreEqual(0, table.Count);
    }

    private void AssertTableContents(SlimHashTable<string, string> table) {
      var dic = table.ToDictionary(x => x, x => x);
      if (table.Count >= 1) {
        Assert.IsTrue(table.Contains("foo"));
        Assert.IsTrue(dic.ContainsKey("foo"));
      }

      for (var i = 0; i < table.Count - 1; i++) {
        var key = "foo" + (table.Count - 2 - i);
        Assert.IsTrue(table.Contains(key));
        Assert.IsTrue(dic.ContainsKey(key));
      }
    }

    private class Parameters : ISlimHashTableParameters<string, string> {
      public Func<string, string> KeyGetter {
        get { return s => s; }
      }
      public Action Locker {
        get { return () => { }; }
      }

      public Action Unlnlocker {
        get { return () => { }; }
      }
    }
  }
}