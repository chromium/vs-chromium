// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileContentsHash : MefTestBase {
    private CompositionContainer _container;

    [TestInitialize]
    public void Initialize() {
      _container = SetupServerMefContainer();
    }

    [TestCleanup]
    public void Cleanup() {
      _container.Dispose();
    }

    [TestMethod]
    public void HashForEmptyContentsWorks() {
      const string text = "";
      var hash = Utils.CreateFileContentsHash(text);
      Assert.IsNotNull(hash.Value);
      Assert.IsTrue(hash.Value.Length >= 10);
    }

    [TestMethod]
    public void HashForLongContentsWorks() {
      var text = new string('a', 1024 * 1024);
      var hash = Utils.CreateFileContentsHash(text);
      Assert.IsNotNull(hash.Value);
      Assert.IsTrue(hash.Value.Length >= 10);
    }

    [TestMethod]
    public void HashForSameContentsIsEqual() {
      const string text = "";
      var hash1 = Utils.CreateFileContentsHash(text);
      var hash2 = Utils.CreateFileContentsHash(text);
      Assert.IsFalse(object.ReferenceEquals(hash1, hash2));
      Assert.AreEqual(hash1.Value, hash2.Value);
      Assert.AreEqual(hash1.GetHashCode(), hash2.GetHashCode());
      Assert.AreEqual(hash1, hash2);
      Assert.IsTrue(hash1.Equals(hash2));
    }

    [TestMethod]
    public void HashForDifferentContentsIsNotEqual() {
      const string text1 = "abc";
      const string text2 = "cba";
      var hash1 = Utils.CreateFileContentsHash(text1);
      var hash2 = Utils.CreateFileContentsHash(text2);
      Assert.IsFalse(object.ReferenceEquals(hash1, hash2));
      Assert.AreNotEqual(hash1.Value, hash2.Value);
      Assert.AreNotEqual(hash1, hash2);
      Assert.IsFalse(hash1.Equals(hash2));
    }
  }
}
