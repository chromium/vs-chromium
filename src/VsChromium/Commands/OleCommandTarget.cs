// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using VsChromium.Core.Logging;

namespace VsChromium.Commands {
  /// <summary>
  /// Implements IOleCommandTarget from an instance of ICommandTarget.
  /// </summary>
  public class OleCommandTarget : IOleCommandTarget {
    private readonly string _description;
    private readonly ICommandTarget _commandTarget;
    private readonly Impl _impl;
    /// <summary>
    /// The next command target in the chain. The caller is responsible for initializing.
    /// This has to be a public field, because it may have to be assigned from an "out" parameter.
    /// If the property is not set, the default behavior is to return OLECMDERR_E_NOTSUPPORTED from
    /// QueryStatus and Exec.
    /// </summary>
    public IOleCommandTarget NextCommandTarget;

    /// <summary>
    /// Note: description is for debugging purposes only.
    /// </summary>
    public OleCommandTarget(string description, ICommandTarget commandTarget) {
      _description = description;
      _commandTarget = commandTarget;
      _impl = new Impl(this);
    }

    public override string ToString() {
      return string.Format("{0}-{1}", this.GetType().Name, this._description);
    }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
      return OleCommandTargetSpy.WrapQueryStatus(this, _impl, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
      return OleCommandTargetSpy.WrapExec(this, _impl, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
    }

    private class Impl : IOleCommandTarget {
      private readonly OleCommandTarget _owner;

      public Impl(OleCommandTarget owner) {
        _owner = owner;
      }

      public override string ToString() {
        return string.Format("Impl({0})", _owner.ToString());
      }

      public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
        var commandId = new CommandID(pguidCmdGroup, (int)prgCmds[0].cmdID);

        bool isSupported = false;
        try {
          isSupported = _owner._commandTarget.HandlesCommand(commandId);
        }
        catch (Exception e) {
          Logger.LogError(e, "Error in {0}.HandlesCommand.", _owner._commandTarget.GetType().FullName);
        }
        if (!isSupported) {
          if (_owner.NextCommandTarget == null) {
            return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
          } else {
            return OleCommandTargetSpy.WrapQueryStatus(_owner, _owner.NextCommandTarget, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
          }
        }

        bool isEnabled = _owner._commandTarget.IsEnabled(commandId);

        prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
        if (isEnabled)
          prgCmds[0].cmdf |= (uint)(OLECMDF.OLECMDF_ENABLED);
        return VSConstants.S_OK;
      }

      public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
        var commandId = new CommandID(pguidCmdGroup, (int)nCmdID);

        bool isSupported = _owner._commandTarget.HandlesCommand(commandId);
        if (!isSupported) {
          if (_owner.NextCommandTarget == null) {
            return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
          } else {
            return OleCommandTargetSpy.WrapExec(_owner, _owner.NextCommandTarget, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
          }
        }

        try {
          _owner._commandTarget.Execute(commandId);
          return VSConstants.S_OK;
        }
        catch (Exception e) {
          Logger.LogError(e, "Error executing editor command.");
          return Marshal.GetHRForException(e);
        }
      }
    }
  }
}
