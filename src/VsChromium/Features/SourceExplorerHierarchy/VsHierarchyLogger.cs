// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.Logging;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyLogger {
    private readonly VsHierarchy _vsHierarchy;
    private static readonly Type[] VshPropTypes = {
      typeof(__VSHPROPID),
      typeof(__VSHPROPID2), 
      typeof(__VSHPROPID3), 
      typeof(__VSHPROPID4), 
      typeof(__VSHPROPID5), 
      typeof(__VSHPROPID6)
    };

    public VsHierarchyLogger(VsHierarchy vsHierarchy) {
      _vsHierarchy = vsHierarchy;
      //this.Enabled = true;
      //this.PropIdEnabled = true;
      //this.PropGuidEnabled = true;
      //this.CommandsEnabled = true;
    }

    public bool Enabled { get; set; }
    public bool PropIdEnabled { get; set; }
    public bool PropGuidEnabled { get; set; }
    public bool CommandsEnabled { get; set; }

    public void Log(string format, params object[] args) {
      if (Enabled) {
        Logger.LogInfo("VsHierarchy: {0}", string.Format(format, args));
      }
    }

    public void LogProperty(string message, uint itemid, int propid) {
      if (PropIdEnabled) {
        Log("{0}({1}) - {2}", message, unchecked((int)itemid), GetEnumName(propid, VshPropTypes));
      }
    }

    public void LogPropertyGuid(string message, uint itemid, int propid) {
      if (PropGuidEnabled) {
        Log("{0}({1}) - {2}", message, unchecked((int)itemid), GetEnumName(propid, VshPropTypes));
      }
    }

    private string GetEnumName(int value, params Type[] enumTypes) {
      var name =
        enumTypes.Select(enumType => Enum.GetName(enumType, value)).FirstOrDefault(x => x != null);
      if (name != null)
        return name;
      return value.ToString();
    }

    public void LogExecCommand(uint itemid, Guid pguidCmdGroup, uint nCmdId, uint nCmdexecopt) {
      if (CommandsEnabled) {
        Log("Exec: {0}-{1}-{2}", pguidCmdGroup, nCmdId, nCmdexecopt);
      }
    }

    public void LogQueryStatusCommand(uint itemid, Guid pguidCmdGroup, uint cmdId) {
      if (CommandsEnabled) {
        Log("QueryStatus: {0}-{1}-{2}", pguidCmdGroup, cmdId);
      }
    }
  }
}
