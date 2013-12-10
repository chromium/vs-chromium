// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromiumCore;

namespace VsChromiumPackage.Package.CommandHandlers {
  [Export(typeof(IPackageCommandHandlerRegistration))]
  public class PackageCommandHandlerRegistration : IPackageCommandHandlerRegistration {
    private readonly IEnumerable<IPackageCommandHandler> _commandHandlers;
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public PackageCommandHandlerRegistration([ImportMany] IEnumerable<IPackageCommandHandler> commandHandlers, IVisualStudioPackageProvider visualStudioPackageProvider) {
      _commandHandlers = commandHandlers;
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public void RegisterCommandHandlers() {
      var mcs = _visualStudioPackageProvider.Package.OleMenuCommandService;
      if (mcs == null) {
        Logger.LogError("Error getting instance of OleMenuCommandService");
        return;
      }

      foreach (var handler in _commandHandlers) {
        // Create the command for the tool window
        var command = new MenuCommand(handler.Execute, handler.CommandId);
        mcs.AddCommand(command);
      }
    }
  }
}
