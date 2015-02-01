// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Views;

namespace VsChromium.Package {
  [Export(typeof(IPackagePostInitializer))]
  public class VsSolutionEventsHandler : IPackagePostInitializer {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IFileSystem _fileSystem;
    private readonly IFileRegistrationRequestService _fileRegistrationRequestService;

    [ImportingConstructor]
    public VsSolutionEventsHandler(
      IVisualStudioPackageProvider visualStudioPackageProvider,
      IFileSystem fileSystem,
      IFileRegistrationRequestService fileRegistrationRequestService) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _fileSystem = fileSystem;
      _fileRegistrationRequestService = fileRegistrationRequestService;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      // If there is a solution already open, open its file in the server
      RegisterCurrentSolution();

      // Advise solution open/close events so that we can notify the server
      ListeToSolutionEvents();
    }

    private void OnAfterOpenSolution() {
      RegisterCurrentSolution();
    }

    private void OnBeforeCloseSolution() {
      UnregisterCurrentSolution();
    }

    private void RegisterCurrentSolution() {
      var vsSolution2 = GetVsSolution();
      if (vsSolution2 == null)
        return;

      if (!IsSolutionOpen(vsSolution2))
        return;

      var path = GetSolutionPath(vsSolution2);
      if (path == null)
        return;

      _fileRegistrationRequestService.RegisterFile(path.Value.Value);
    }

    private void UnregisterCurrentSolution() {
      var vsSolution2 = GetVsSolution();
      if (vsSolution2 == null)
        return;

      var path = GetSolutionPath(vsSolution2);
      if (path == null)
        return;

      _fileRegistrationRequestService.UnregisterFile(path.Value.Value);
    }

    private void ListeToSolutionEvents() {
      var vsSolution2 = GetVsSolution();
      if (vsSolution2 == null)
        return;

      var handler = new SolutionEventsHandler(this);
      uint cookie;
      var hr = vsSolution2.AdviseSolutionEvents(handler, out cookie);
      if (ErrorHandler.Succeeded(hr)) {
        _visualStudioPackageProvider.Package.DisposeContainer.Add(
          () => vsSolution2.UnadviseSolutionEvents(cookie));
      }
    }

    private IVsSolution2 GetVsSolution() {
      var vsSolution2 = _visualStudioPackageProvider
        .Package
        .ServiceProvider
        .GetService(typeof (SVsSolution)) as IVsSolution2;
      return vsSolution2;
    }

    private static bool IsSolutionOpen(IVsSolution2 vsSolution2) {
      object pvar;
      if (!ErrorHandler.Succeeded(
            vsSolution2.GetProperty((int) __VSPROPID.VSPROPID_IsSolutionOpen,
            out pvar))) {
        return false;
      }

      return (pvar is bool) && ((bool) pvar);
    }

    private FullPath? GetSolutionPath(IVsSolution2 vsSolution2) {
      object pvar;
      if (!ErrorHandler.Succeeded(vsSolution2.GetProperty((int)__VSPROPID.VSPROPID_SolutionFileName, out pvar))) {
        return null;
      }

      var name = pvar as string;
      if (name == null)
        return null;

      if (!PathHelpers.IsAbsolutePath(name))
        return null;

      var path = new FullPath(name);
      if (!_fileSystem.FileExists(path))
        return null;

      return path;
    }

    internal class SolutionEventsHandler : IVsSolutionEvents {
      private readonly VsSolutionEventsHandler _vsSolutionEventsHandler;

      internal SolutionEventsHandler(VsSolutionEventsHandler vsSolutionEventsHandler) {
        _vsSolutionEventsHandler = vsSolutionEventsHandler;
      }

      public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution) {
        Logger.WrapActionInvocation(() =>
          _vsSolutionEventsHandler.OnAfterOpenSolution());
        return VSConstants.S_OK;
      }

      public int OnBeforeCloseSolution(object pUnkReserved) {
        Logger.WrapActionInvocation(() =>
          _vsSolutionEventsHandler.OnBeforeCloseSolution());
        return VSConstants.S_OK;
      }

      public int OnAfterCloseSolution(object pUnkReserved) {
        return VSConstants.S_OK;
      }

      public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) {
        return VSConstants.S_OK;
      }

      public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) {
        return VSConstants.S_OK;
      }

      public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) {
        return VSConstants.S_OK;
      }

      public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) {
        return VSConstants.S_OK;
      }

      public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) {
        return VSConstants.S_OK;
      }

      public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) {
        return VSConstants.S_OK;
      }

      public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) {
        return VSConstants.S_OK;
      }
    }
  }
}
