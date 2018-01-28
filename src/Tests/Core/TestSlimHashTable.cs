// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using VsChromium.Core.Collections;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestSlimHashTable {
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestAddException() {
      var table = new SlimHashTable<string, string>(x => x, 10);
      table.Add("10");
      table.Add("10");
    }

    [TestMethod]
    public void TestIndexerUpdateNoException() {
      var table = new SlimHashTable<string, string>(x => x, 10);
      table["10"] = "10";
      table["10"] = "10";
      Assert.AreEqual(1, table.Count);
    }

    [TestMethod]
    public void Test_Add_Remove_CopyTo_Keys_Values() {
      var table = new SlimHashTable<string, string>(x => x, 10);
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

    [TestMethod]
    public void TestUpdateValueType() {
      var table = new SlimHashTable<string, MyTestValueType>(x => x.Key, 10);
      table.Add(new MyTestValueType("test", 5));

      Assert.IsTrue(table.Contains("test"));
      Assert.AreEqual(5, table["test"].Value);

      table["test"] = new MyTestValueType("test", 15);

      Assert.IsTrue(table.Contains("test"));
      Assert.AreEqual(15, table["test"].Value);
    }

    [TestMethod]
    public void TestGetOrAdd() {
      var table = new SlimHashTable<string, MyTestType>(x => x.Key, 10);

      var item = new MyTestType("test", 5);
      var result = table.GetOrAdd(item.Key, item);
      Assert.IsTrue(ReferenceEquals(item, result));

      var item2 = new MyTestType("test", 5);
      var result2 = table.GetOrAdd(item2.Key, item2);
      Assert.IsTrue(ReferenceEquals(item, result2));
    }

    [TestMethod]
    public void TestUpdateOrAdd() {
      var table = new SlimHashTable<string, MyTestType>(x => x.Key, 10);

      var item = new MyTestType("test", 5);
      table.UpdateOrAdd(item.Key, item);
      Assert.IsTrue(ReferenceEquals(item, table[item.Key]));

      var item2 = new MyTestType("test", 5);
      table.UpdateOrAdd(item2.Key, item2);
      Assert.IsTrue(ReferenceEquals(item2, table[item.Key]));
    }

    [TestMethod]
    public void TestGetHashCodeCalls() {
      var table = new SlimHashTable<MyTestType, MyTestType>(x => x, 10);

      var item = new MyTestType("test", 5);
      Assert.AreEqual(0, item.HashCodeCallCount);
      table.Add(item);
      Assert.AreEqual(1, item.HashCodeCallCount);

      // Add 100 elements to force a grow call
      for (var i = 0; i < 100; i++) {
        table.Add(new MyTestType(i.ToString(), i));
      }

      // Assert
      Assert.AreEqual(1, item.HashCodeCallCount);
    }

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    private class MyTestType :IEquatable<MyTestType> {
      public readonly string Key;
      public readonly int Value;
      public int HashCodeCallCount;

      public MyTestType(string key, int value) {
        Key = key;
        Value = value;
      }

      public bool Equals(MyTestType other) {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Key, other.Key);
      }

      public override bool Equals(object obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((MyTestType) obj);
      }

      [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
      public override int GetHashCode() {
        HashCodeCallCount++;
        return Key.GetHashCode();
      }
    }

    private struct MyTestValueType {
      public readonly string Key;
      public readonly int Value;

      public MyTestValueType(string key, int value) {
        Key = key;
        Value = value;
      }
    }

    private void AssertTableContents(SlimHashTable<string, string> table) {
      var dic = table.ToDictionary(x => x.Key, x => x.Value);
      var keySet = new HashSet<string>(table.Keys);
      var valueSet = new HashSet<string>(table.Values);
      var kvp = new KeyValuePair<string, string>[table.Count];
      table.CopyTo(kvp, 0);
      var dic2 = kvp.ToDictionary(x => x.Key, x => x.Value);

      if (table.Count >= 1) {
        Assert.IsTrue(table.Contains("foo"));
        Assert.IsTrue(dic.ContainsKey("foo"));
        Assert.IsTrue(dic2.ContainsKey("foo"));
        Assert.IsTrue(keySet.Contains("foo"));
        Assert.IsTrue(valueSet.Contains("foo"));
      }

      for (var i = 0; i < table.Count - 1; i++) {
        var key = "foo" + (table.Count - 2 - i);
        Assert.IsTrue(table.Contains(key));
        Assert.IsTrue(dic.ContainsKey(key));
        Assert.IsTrue(dic2.ContainsKey(key));
        Assert.IsTrue(keySet.Contains(key));
        Assert.IsTrue(valueSet.Contains(key));
      }
    }
  }
}