using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using VsChromiumCore;

namespace VsChromiumPackage.Commands {
  public class CommandTargetWrapper : IOleCommandTarget {
    private readonly ICommandTarget _commandTarget;

    public CommandTargetWrapper(ICommandTarget commandTarget) {
      this._commandTarget = commandTarget;
    }

    public IOleCommandTarget NextCommandTarget;

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
      var commandId = new CommandID(pguidCmdGroup, (int)prgCmds[0].cmdID);

      bool isSupported = this._commandTarget.HandlesCommand(commandId);
      if (!isSupported) {
        return this.NextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
      }

      bool isEnabled = this._commandTarget.IsEnabled(commandId);

      prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
      if (isEnabled)
        prgCmds[0].cmdf |= (uint)(OLECMDF.OLECMDF_ENABLED);
      return VSConstants.S_OK;
    }

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
      var commandId = new CommandID(pguidCmdGroup, (int)nCmdID);

      bool isSupported = this._commandTarget.HandlesCommand(commandId);
      if (!isSupported) {
        return this.NextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
      }

      try {
        this._commandTarget.Execute(commandId);
        return VSConstants.S_OK;
      }
      catch (Exception e) {
        Logger.LogException(e, "Error executing editor command.");
        return Marshal.GetHRForException(e);
      }
    }
  }
}