// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server;
using VsChromium.Server.Ipc.ProtocolHandlers;
using VsChromium.Server.Ipc.TypedMessageHandlers;
using VsChromium.Server.Projects;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestServerServices {
    [TestMethod]
    public void AllServicesCanBeCreated() {
      // Note: ServerServices builds the provider with "ValidateOnBuild", which checks that the
      // constructor dependencies of all the services can be resolved.
      using (var provider = ServerServices.CreateServiceProvider()) {
        Assert.IsNotNull(provider.GetRequiredService<IServer>());
      }
    }

    [TestMethod]
    public void AllImplementationsAreRegistered() {
      using (var provider = ServerServices.CreateServiceProvider()) {
        // One request handler per typed request (see Core.Ipc TypedRequest sub-classes handled by
        // the server).
        var handlers = provider.GetServices<ITypedMessageRequestHandler>().ToList();
        Assert.AreEqual(19, handlers.Count);
        Assert.AreEqual(handlers.Count, handlers.Select(x => x.GetType()).Distinct().Count());

        Assert.AreEqual(3, provider.GetServices<IProtocolHandler>().Count());
        Assert.AreEqual(2, provider.GetServices<IProjectDiscoveryProvider>().Count());
      }
    }

    [TestMethod]
    public void AllRequestHandlerClassesAreRegistered() {
      // Guard against adding a new request handler class without registering it.
      var handlerClasses = typeof(ITypedMessageRequestHandler).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(ITypedMessageRequestHandler).IsAssignableFrom(t))
        .ToList();
      using (var provider = ServerServices.CreateServiceProvider()) {
        var registered = provider.GetServices<ITypedMessageRequestHandler>().Select(x => x.GetType()).ToList();
        CollectionAssert.AreEquivalent(handlerClasses, registered);
      }
    }
  }
}
