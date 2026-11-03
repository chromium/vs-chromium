// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.Json;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server;
using VsChromium.Server.Ipc.TypedMessageHandlers;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestServerComponents {
    [TestMethod]
    [DataRow(ServerProtocolMode.TypedMessages)]
    [DataRow(ServerProtocolMode.JsonRpc)]
    public void AllComponentsCanBeCreated(ServerProtocolMode mode) {
      using (var components = new ServerComponents(mode)) {
        if (mode == ServerProtocolMode.TypedMessages) {
          Assert.IsNotNull(components.Server);
        } else {
          Assert.IsNotNull(components.JsonRpcServer);
        }
        // All the public components (properties) of the composition root: the components of the
        // other protocol are not available.
        var unavailable = 0;
        foreach (var property in typeof(ServerComponents).GetProperties()) {
          try {
            Assert.IsNotNull(property.GetValue(components), property.Name);
          }
          catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) {
            unavailable++;
          }
        }
        Assert.IsGreaterThan(0, unavailable);
      }
    }

    [TestMethod]
    public void ComponentsOfTheOtherProtocolAreNotAvailable() {
      using (var components = new ServerComponents(ServerProtocolMode.JsonRpc)) {
        Assert.ThrowsExactly<InvalidOperationException>(() => components.Server);
        Assert.ThrowsExactly<InvalidOperationException>(() => components.TypedEventSender);
        Assert.IsInstanceOfType(components.ProgressEventSender, typeof(VsChromium.Server.JsonRpc.JsonRpcNotificationSender));
      }
      using (var components = new ServerComponents(ServerProtocolMode.TypedMessages)) {
        Assert.ThrowsExactly<InvalidOperationException>(() => components.JsonRpcServer);
        Assert.ThrowsExactly<InvalidOperationException>(() => components.ProtocolRequestHandler);
        Assert.IsInstanceOfType(components.ProgressEventSender, typeof(VsChromium.Server.Ipc.TypedEvents.TypedEventProgressSender));
      }
    }

    [TestMethod]
    public void AllImplementationsAreRegistered() {
      using (var components = new ServerComponents(ServerProtocolMode.TypedMessages)) {
        var handlers = components.TypedMessageRequestHandlers;
        Assert.AreEqual(20, handlers.Count);
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
      using (var components = new ServerComponents(ServerProtocolMode.TypedMessages)) {
        var registered = components.TypedMessageRequestHandlers.Select(x => x.GetType()).ToList();
        CollectionAssert.AreEquivalent(handlerClasses, registered);
      }
    }

    [TestMethod]
    public void EachRequestHasExactlyOneHandler() {
      using (var components = new ServerComponents(ServerProtocolMode.TypedMessages)) {
        foreach (var requestType in MessageTypes.All.Where(t => !t.IsAbstract && t != typeof(TypedRequest) && typeof(TypedRequest).IsAssignableFrom(t))) {
          var request = (TypedRequest)System.Activator.CreateInstance(requestType);
          var count = components.TypedMessageRequestHandlers.Count(x => x.CanProcess(request));
          Assert.AreEqual(1, count, requestType.Name);
        }
      }
    }
  }
}
