// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestServerComponents {
    [TestMethod]
    public void AllComponentsCanBeCreated() {
      using (var components = new ServerComponents()) {
        Assert.IsNotNull(components.JsonRpcServer);
        // All the public components (properties) of the composition root.
        foreach (var property in typeof(ServerComponents).GetProperties()) {
          Assert.IsNotNull(property.GetValue(components), property.Name);
        }
      }
    }

    [TestMethod]
    public void AllImplementationsAreRegistered() {
      using (var components = new ServerComponents()) {
        Assert.AreEqual(2, components.ProjectDiscoveryProviders.Count);
      }
    }
  }
}
