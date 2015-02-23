// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Commands;
using VsChromium.Core.Logging;
using VsChromium.Features.ToolWindows.BuildExplorer;
using VsChromium.Features.ToolWindows.CodeSearch;
using VsChromium.Package;
using VsChromium.ToolsOptions;
using IServiceProvider = System.IServiceProvider;

namespace VsChromium {
  [PackageRegistration(UseManagedResourcesOnly = true)]
  [InstalledProductRegistration("#110", "#112", VsChromium.Core.VsChromiumVersion.Product, IconResourceID = 400)]
  // When in development mode, update the version # below every time there is a change to the .VSCT file,
  // or Visual Studio won't take into account the changes (this is true with VS 2010, maybe not with
  // VS 2012 and later since package updates is more explicit).
  [ProvideMenuResource("Menus.ctmenu", 12)]
  [ProvideToolWindow(typeof(SourceExplorerToolWindow))]
  [ProvideToolWindow(typeof(BuildExplorerToolWindow))]
  [Guid(GuidList.GuidVsChromiumPkgString)]
  [ProvideOptionPage(
    typeof(GeneralOptions), // Type of page to open
    "VS Chromium", // Non localized version of the top level category
    "General", // Non localized version of the page name within the category
    210, // Localized resource id of the top level category 
    211, // Loalized resource id of the page name within the category
    true, // Support automation
    // List of keywords for Tools|Options search
    new []{"VS Chromium", "Chrome", "Code Search", "Solution Explorer", "Debugging", "Search", "Index"},
    SupportsProfiles = true)
  ]
  [ProvideOptionPage(
    typeof(DebuggingOptions), // Type of page to open
    "VS Chromium", // Non localized version of the top level category
    "Debugging", // Non localized version of the page name within the category
    210, // Localized resource id of the top level category 
    212, // Loalized resource id of the page name within the category
    true, // Support automation
    // List of keywords for Tools|Options search
    new[] { "VS Chromium", "Chrome", "Debugging" },
    SupportsProfiles = true)
  ]
  [ProvideOptionPage(
    typeof(CodingStyleOptions), // Type of page to open
    "VS Chromium", // Non localized version of the top level category
    "Coding Style", // Non localized version of the page name within the category
    210, // Localized resource id of the top level category 
    213, // Loalized resource id of the page name within the category
    true, // Support automation
    // List of keywords for Tools|Options search
    new[] { "VS Chromium", "Chrome", "Coding Style", "Style" },
    SupportsProfiles = true)
  ]
  public sealed class VsPackage : Microsoft.VisualStudio.Shell.Package, IVisualStudioPackage, IOleCommandTarget {
    private readonly IDisposeContainer _disposeContainer = new DisposeContainer();

    public VsPackage() {
      Logger.LogInfo("{0} constructor.", this.GetType().FullName);
    }

    private static bool _loaded = false;
    public static void EnsureLoaded() {
      // Try loading only once since this is a heavy operation.
      if (_loaded)
        return;
      _loaded = true;

      Logger.WrapActionInvocation(
        () => {
          var shell = GetGlobalService(typeof (SVsShell)) as IVsShell;
          if (shell == null)
            return;

          IVsPackage package;
          var packageToBeLoadedGuid = new Guid(GuidList.GuidVsChromiumPkgString);
          shell.LoadPackage(ref packageToBeLoadedGuid, out package);
        });
    }

    public IComponentModel ComponentModel {
      get {
        return (IComponentModel) GetService(typeof (SComponentModel));
      }
    }

    public OleMenuCommandService OleMenuCommandService {
      get {
        return GetService(typeof (IMenuCommandService)) as OleMenuCommandService;
      }
    }

    public IVsRegisterPriorityCommandTarget VsRegisterPriorityCommandTarget {
      get { return GetService(typeof(SVsRegisterPriorityCommandTarget)) as IVsRegisterPriorityCommandTarget; }
    }

    public IVsUIShell VsUIShell {
      get {
        return GetService(typeof (SVsUIShell)) as IVsUIShell;
      }
    }

    public EnvDTE.DTE DTE {
      get {
        return (EnvDTE.DTE) GetService(typeof (EnvDTE.DTE));
      }
    }

    public IServiceProvider ServiceProvider {
      get {
        return this;
      }
    }

    public Microsoft.VisualStudio.OLE.Interop.IServiceProvider InteropServiceProvider {
      get {
        return this;
      }
    }

    public IDisposeContainer DisposeContainer {
      get { return _disposeContainer; }
    }

    public T GetToolsOptionsPage<T>() where T : DialogPage {
      return (T)GetDialogPage(typeof(T));
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        Logger.LogInfo("{0}.Dispose()", this.GetType().FullName);
        try {
          if (ComponentModel != null) {
            var exports = ComponentModel.DefaultExportProvider.GetExportedValues<IPackagePostDispose>();
            foreach (var disposer in exports.OrderByDescending(x => x.Priority)) {
              disposer.Run(this);
            }
          }
          _disposeContainer.RunAll();
        }
        catch (Exception e) {
          Logger.LogError(e, "Error disposing VsChromium package.");
          //throw;
        }
      }
      base.Dispose(disposing);
    }

    protected override void Initialize() {
      Logger.LogInfo("{0}.Initialize()", this.GetType().FullName);

      base.Initialize();
      try {
        PreInitialize();
        PostInitialize();
      }
      catch (Exception e) {
        Logger.LogError(e, "Error initializing VsChromium package.");
        throw;
      }
    }

    private void PreInitialize() {
      foreach(var initializer in ComponentModel.DefaultExportProvider.GetExportedValues<IPackagePreInitializer>().OrderByDescending(x=> x.Priority)) {
        initializer.Run(this);
      }
    }

    private void PostInitialize() {
      foreach (var initializer in ComponentModel.DefaultExportProvider.GetExportedValues<IPackagePostInitializer>().OrderByDescending(x => x.Priority)) {
        initializer.Run(this);
      }

      //var serviceContainer = this as IServiceContainer;
      //serviceContainer.AddService(typeof(IOleCommandTarget), new OleCommandTarget(new PackageCommandTarget()));
    }

    int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
      var impl = this.GetService(typeof(IMenuCommandService)) as IOleCommandTarget;
      return OleCommandTargetSpy.WrapQueryStatus(this, impl, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }

    int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
      var impl = this.GetService(typeof(IMenuCommandService)) as IOleCommandTarget;
      return OleCommandTargetSpy.WrapExec(this, impl, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
    }
  }
}
