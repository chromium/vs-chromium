// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.Extensions.DependencyInjection;
using VsChromium.Server;

namespace VsChromium.Tests {
  public class ServerServicesTestBase {
    /// <summary>
    /// The services of the server (in process), as created by ServerStub.
    /// </summary>
    protected static ServiceProvider CreateServerServices() {
      return ServerServices.CreateServiceProvider();
    }
  }
}
