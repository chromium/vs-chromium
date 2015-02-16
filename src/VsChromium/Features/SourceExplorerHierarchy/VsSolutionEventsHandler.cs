// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.Logging;
using VsChromium.Package;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsSolutionEventsHandler : IVsSolutionEventsHandler {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    public VsSolutionEventsHandler(
      IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;

      // Advise solution open/close events so that we can notify the server
      ListenToSolutionEvents();
    }

    public bool IsSolutionOpen {
      get { return HierarchyUtilities.IsSolutionOpen; }
    }

    public event Action AfterOpenSolution;

    public void OnAfterOpenSolution() {
      var handler = AfterOpenSolution;
      if (handler != null) handler();
    }

    public event Action BeforeCloseSolution;

    public void OnBeforeCloseSolution() {
      var handler = BeforeCloseSolution;
      if (handler != null) handler();
    }

    private void ListenToSolutionEvents() {
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

    private class SolutionEventsHandler : IVsSolutionEvents {
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
