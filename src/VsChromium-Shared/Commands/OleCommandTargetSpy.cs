// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Design;
using System.Diagnostics;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Logging;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Commands {
  /// <summary>
  /// Enables spying and debugging of any IOleCommandTarget implementation.
  /// </summary>
  public static class OleCommandTargetSpy {
    /// <summary>
    /// Returns true if <paramref name="commandId"/> activity should be logged.
    /// Note: Update this method for debugging any command routed throughout the
    /// VsChromium package.
    /// </summary>
    private static bool LogCommand(CommandID commandId) {
      //if (commandId.Equals(new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.NextLocation)))
      //  return true;

      return false;
    }

    public static void WrapBeforeQueryStatus(OleMenuCommand command, IPackageCommandHandler handler) {
      command.Supported = handler.Supported;
      if (!command.Supported) {
        // Ensures OleMenuCommandService returns OLECMDERR_E_NOTSUPPORTED.
        command.Enabled = false;
        command.Checked = false;
        command.Visible = true;
      } else {
        command.Checked = handler.Checked;
        command.Enabled = handler.Enabled;
        command.Visible = handler.Visible;
      }

      if (LogCommand(command.CommandID)) {
        Logger.LogInfo(
          "BeforeQueryStatus: cmd={0}, handler={1}: Supported={2}, Checked={3}, Enabled={4}, Visible={5}",
          command,
          handler,
          command.Supported,
          command.Checked,
          command.Enabled,
          command.Visible);
      }
    }

    public static int WrapQueryStatus(
      IOleCommandTarget receiver,
      IOleCommandTarget implementer,
      ref System.Guid pguidCmdGroup,
      uint cCmds,
      OLECMD[] prgCmds,
      System.IntPtr pCmdText) {
      Invariants.Assert(receiver != null);

      var commandId = new CommandID(pguidCmdGroup, (int)prgCmds[0].cmdID);
      if (LogCommand(commandId)) {
        Logger.LogInfo("WrapQueryStatus: => recv={0}, impl={1}, parent={2}",
          receiver,
          GetImplementerString(implementer),
          GetParentTargetString(implementer));
      }

      var hr = (implementer == null)
        ? (int)Constants.OLECMDERR_E_NOTSUPPORTED
        : implementer.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

      if (LogCommand(commandId)) {
        Logger.LogInfo("WrapQueryStatus: <= recv={0}, impl={1}, parent={2}, hr={3}, cmdf={4}",
          receiver,
          GetImplementerString(implementer),
          GetParentTargetString(implementer),
          HrToString(hr),
          CmdFlagsToString(prgCmds));
      }
      return hr;
    }

    public static int WrapExec(
      IOleCommandTarget receiver,
      IOleCommandTarget implementer,
      ref System.Guid pguidCmdGroup,
      uint nCmdID,
      uint nCmdexecopt,
      System.IntPtr pvaIn,
      System.IntPtr pvaOut) {
        Invariants.Assert(receiver != null);

      var commandId = new CommandID(pguidCmdGroup, (int)nCmdID);
      if (LogCommand(commandId)) {
        Logger.LogInfo("WrapExec: => recv={0}, impl={1}, parent={2}",
          receiver,
          GetImplementerString(implementer),
          GetParentTargetString(implementer));
      }

      var hr = (implementer == null)
        ? (int)Constants.OLECMDERR_E_NOTSUPPORTED
        : implementer.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
      // Ensures OleMenuCommandService returns OLECMDERR_E_NOTSUPPORTED.
      if (hr == VSConstants.S_OK && implementer is OleMenuCommandService) {
        // Ensure we return OLECMDERR_E_NOTSUPPORTED instead of S_OK if our
        // command does not support the command id. This is necessary so that
        // the VS Shell chains the Exec call to other IOleCommandTarget
        // implementations.
        var service = (OleMenuCommandService)implementer;
        var command = service.FindCommand(commandId);
        if (command != null) {
          if (!command.Supported) {
            hr = (int)Constants.OLECMDERR_E_NOTSUPPORTED;
          }
        }
      }

      if (LogCommand(commandId)) {
        Logger.LogInfo("WrapExec: <= recv={0}, impl={1}, parent={2}, hr={3}",
          receiver,
          GetImplementerString(implementer),
          GetParentTargetString(implementer),
          HrToString(hr));
      }
      return hr;
    }

    private static uint CmdFlagsToString(OLECMD[] prgCmds) {
      return prgCmds[0].cmdf;
    }

    private static string HrToString(int hr) {
      return hr == VSConstants.S_OK
        ? "S_OK"
        : hr == (int)Constants.OLECMDERR_E_NOTSUPPORTED
          ? "OLECMDERR_E_NOTSUPPORTED"
          : hr.ToString();
    }

    private static string GetImplementerString(IOleCommandTarget implementer) {
      return (implementer == null ? "null" : implementer.ToString());
    }

    private static string GetParentTargetString(IOleCommandTarget implementer) {
      var oleMenuCommandService = (implementer as OleMenuCommandService);
      if (oleMenuCommandService == null || oleMenuCommandService.ParentTarget == null) {
        return "null";
      }

      return oleMenuCommandService.ParentTarget.ToString();
    }
  }
}