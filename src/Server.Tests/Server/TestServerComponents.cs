// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.Json;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server;
using VsChromium.Server.Ipc.TypedMessageHandlers;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestServerComponents {
    [TestMethod]
    public void AllComponentsCanBeCreated() {
      using (var components = new ServerComponents()) {
        Assert.IsNotNull(components.Server);
        // All the public components (properties) of the composition root.
        foreach (var property in typeof(ServerComponents).GetProperties()) {
          Assert.IsNotNull(property.GetValue(components), property.Name);
        }
      }
    }

    [TestMethod]
    public void AllImplementationsAreRegistered() {
      using (var components = new ServerComponents()) {
        var handlers = components.TypedMessageRequestHandlers;
        Assert.AreEqual(19, handlers.Count);
        Assert.AreEqual(handlers.Count, handlers.Select(x => x.GetType()).Distinct().Count());

        Assert.AreEqual(3, components.ProtocolHandlers.Count);
        Assert.AreEqual(2, components.ProjectDiscoveryProviders.Count);
      }
    }

    [TestMethod]
    public void AllRequestHandlerClassesAreRegistered() {
      // (Tests can use reflection.)
      var handlerClasses = typeof(ITypedMessageRequestHandler).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(ITypedMessageRequestHandler).IsAssignableFrom(t))
        .ToList();
      using (var components = new ServerComponents()) {
        var registered = components.TypedMessageRequestHandlers.Select(x => x.GetType()).ToList();
        CollectionAssert.AreEquivalent(handlerClasses, registered);
      }
    }

    [TestMethod]
    public void EachRequestHasExactlyOneHandler() {
      using (var components = new ServerComponents()) {
        foreach (var requestType in MessageTypes.All.Where(t => !t.IsAbstract && t != typeof(TypedRequest) && typeof(TypedRequest).IsAssignableFrom(t))) {
          var request = (TypedRequest)System.Activator.CreateInstance(requestType);
          var count = components.TypedMessageRequestHandlers.Count(x => x.CanProcess(request));
          Assert.AreEqual(1, count, requestType.Name);
        }
      }
    }
  }
}
