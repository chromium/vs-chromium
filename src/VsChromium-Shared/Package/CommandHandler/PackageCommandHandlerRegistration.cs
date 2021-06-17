﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Package.CommandHandler {
  [Export(typeof(IPackagePostInitializer))]
  public class PackageCommandHandlerRegistration : IPackagePostInitializer {
    private readonly IEnumerable<IPackageCommandHandler> _commandHandlers;

    [ImportingConstructor]
    public PackageCommandHandlerRegistration([ImportMany] IEnumerable<IPackageCommandHandler> commandHandlers) {
      _commandHandlers = commandHandlers;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      var mcs = package.OleMenuCommandService;
      if (mcs == null) {
        Logger.LogError("Error getting instance of OleMenuCommandService");
        return;
      }

      _commandHandlers.ForAll(handler =>
        mcs.AddCommand(handler.ToOleMenuCommand()));
    }
  }
}
  