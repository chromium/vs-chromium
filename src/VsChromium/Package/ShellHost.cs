// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Composition;

namespace VsChromium.Package {
  [Export(typeof(IShellHost))]
  public class ShellHost : IShellHost {
    private readonly IVisualStudioPackageProvider _packageProvider;

    [ImportingConstructor]
    public ShellHost(IVisualStudioPackageProvider packageProvider) {
      _packageProvider = packageProvider;
    }

    public void ShowInfoMessageBox(string title, string message) {
      var serviceProvider = _packageProvider.Package.ServiceProvider;
      VsShellUtilities.ShowMessageBox(serviceProvider, message, title,
        OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }

    public void ShowErrorMessageBox(string title, string message) {
      var serviceProvider = _packageProvider.Package.ServiceProvider;
      VsShellUtilities.ShowMessageBox(serviceProvider, message, title,
        OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
  }
}