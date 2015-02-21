// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VsChromium.Package {
  public interface IVisualStudioPackage {
    IComponentModel ComponentModel { get; }
    OleMenuCommandService OleMenuCommandService { get; }
    IVsRegisterPriorityCommandTarget VsRegisterPriorityCommandTarget { get; }
    IVsUIShell VsUIShell { get; }
    EnvDTE.DTE DTE { get; }
    IServiceProvider ServiceProvider { get; }
    Microsoft.VisualStudio.OLE.Interop.IServiceProvider InteropServiceProvider { get; }
    IDisposeContainer DisposeContainer { get; }

    ToolWindowPane FindToolWindow(Type toolWindowType, int id, bool create);
  }
}
