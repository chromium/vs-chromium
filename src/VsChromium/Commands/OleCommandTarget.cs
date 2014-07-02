// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using VsChromium.Core;
using VsChromium.Core.Logging;

namespace VsChromium.Commands {
  /// <summary>
  /// Implements IOleCommandTarget from an instance of ICommandTarget.
  /// </summary>
  public class OleCommandTarget : IOleCommandTarget {
    private readonly ICommandTarget _commandTarget;

    public OleCommandTarget(ICommandTarget commandTarget) {
      _commandTarget = commandTarget;
    }

    /// <summary>
    /// The next command target in the chain. The caller is responsible for initializing.
    /// This has to be a public field, because it may have to be assigned from an "out" parameter.
    /// </summary>
    public IOleCommandTarget NextCommandTarget;

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
      var commandId = new CommandID(pguidCmdGroup, (int)prgCmds[0].cmdID);

      bool isSupported = false;
      try {
        isSupported = _commandTarget.HandlesCommand(commandId);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error in {0}.HandlesCommand.", _commandTarget.GetType().FullName);
      }
      if (!isSupported) {
        return NextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
      }

      bool isEnabled = _commandTarget.IsEnabled(commandId);

      prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
      if (isEnabled)
        prgCmds[0].cmdf |= (uint)(OLECMDF.OLECMDF_ENABLED);
      return VSConstants.S_OK;
    }

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
      var commandId = new CommandID(pguidCmdGroup, (int)nCmdID);

      bool isSupported = _commandTarget.HandlesCommand(commandId);
      if (!isSupported) {
        return NextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
      }

      try {
        _commandTarget.Execute(commandId);
        return VSConstants.S_OK;
      }
      catch (Exception e) {
        Logger.LogException(e, "Error executing editor command.");
        return Marshal.GetHRForException(e);
      }
    }
  }
}
