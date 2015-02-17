// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Commands;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

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

    private class MenuGroupDefinition {
      public MemberInfo MemberInfo { get; set; }

      public Guid Guid {
        get {
          var propertyInfo = MemberInfo as PropertyInfo;
          if (propertyInfo != null) {
            return (Guid)propertyInfo.GetValue(null);
          }
          var fieldInfo = MemberInfo as FieldInfo;
          if (fieldInfo != null) {
            return (Guid)fieldInfo.GetValue(null);
          }
          return Guid.Empty;
        }
      }

      public Type[] EnumTypes { get; set; }
    }

    private static readonly MenuGroupDefinition[] MenuGroupDefinitions = {
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.VSStd2K), 
        EnumTypes = new[] {typeof(VSConstants.VSStd2KCmdID)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.GUID_VSStandardCommandSet97), 
        EnumTypes = new[] {typeof(VSConstants.VSStd97CmdID)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.GUID_VsUIHierarchyWindowCmds), 
        EnumTypes = new[] {typeof(VSConstants.VsUIHierarchyWindowCmdIds)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => GuidList.GuidVsChromiumCmdSet), 
        EnumTypes = new[] {typeof(PkgCmdIdList)}
      },
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

    public void LogExecCommand(uint itemid, Guid pguidCmdGroup, uint nCmdId, uint nCmdexecopt) {
      if (CommandsEnabled) {
        Log("Exec: {0}", GetCommandName(pguidCmdGroup, nCmdId));
      }
    }

    public void LogQueryStatusCommand(uint itemid, Guid pguidCmdGroup, uint cmdId) {
      if (CommandsEnabled) {
        Log("QueryStatus: {0}", GetCommandName(pguidCmdGroup, cmdId));
      }
    }

    private string GetEnumName(int value, params Type[] enumTypes) {
      var name =
        enumTypes.Select(enumType => Enum.GetName(enumType, value)).FirstOrDefault(x => x != null);
      if (name != null)
        return name;
      return value.ToString();
    }

    private string GetCommandName(Guid commandGroup, uint commandId) {
      var def = MenuGroupDefinitions.FirstOrDefault(x => x.Guid == commandGroup);
      if (def == null) {
        return string.Format("CommandGroup={0}, CommandId={1} (0x{1:x})", commandGroup, commandId);
      }
      var name = GetEnumName((int)commandId, def.EnumTypes);
      return string.Format("{0}-{1} (0x{2:x})", def.MemberInfo.Name, name, commandId);
    }
  }
}
