// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
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

    private class CustomGroupGuids  {
      public static readonly Guid guidDebugPkg = new Guid("430bfe20-7dd4-4875-9854-ef712413d4b0");
      public static readonly Guid guidResharperPkg = new Guid("cb26e292-901a-419c-b79d-49bd45c43929");
      public static readonly Guid guidFSharpProjectCmdSet = new Guid("75AC5611-A912-4195-8A65-457AE17416FB");
      public static readonly Guid guidSqlPkg = new Guid("000af700-cf09-4582-9e1c-2603403ab647");
      public static readonly Guid guidBrowserLinkCmdSet = new Guid("30947ebe-9147-45f9-96cf-401bfc671a82");
      public static readonly Guid guidVenusCmdId = new Guid("c7547851-4e3a-4e5b-9173-fa6e9c8bd82c");

      public static readonly Guid guidUnknown1 = new Guid("25113e5b-9964-4375-9dd1-0a5e9840507a");
      public static readonly Guid guidUnknown2 = new Guid("25113e5b-9964-4375-9dd1-0a5e9840507a");
      
    }

    private static readonly MenuGroupDefinition[] MenuGroupDefinitions = {
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.GUID_AppCommand), 
        EnumTypes = new[] {typeof(VSConstants.AppCommandCmdID)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.VSStd2K), 
        EnumTypes = new[] {typeof(VSConstants.VSStd2KCmdID)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.GUID_VSStandardCommandSet97), 
        EnumTypes = new[] {typeof(VSConstants.VSStd97CmdID)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.VsStd2010), 
        EnumTypes = new[] {typeof(VSConstants.VSStd2010CmdID)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.VsStd11), 
        EnumTypes = new[] {typeof(VSConstants.VSStd11CmdID)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.VsStd12), 
        EnumTypes = new[] {typeof(VSConstants.VSStd12CmdID)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => VSConstants.GUID_VsUIHierarchyWindowCmds), 
        EnumTypes = new[] {typeof(VSConstants.VsUIHierarchyWindowCmdIds)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => GuidList.GuidVsChromiumCmdSet), 
        EnumTypes = new[] {typeof(PkgCmdIdList)}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => CustomGroupGuids.guidDebugPkg), 
        EnumTypes = new Type[] {},
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => CustomGroupGuids.guidResharperPkg), 
        EnumTypes = new Type[] {}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => CustomGroupGuids.guidFSharpProjectCmdSet), 
        EnumTypes = new Type[] {}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => CustomGroupGuids.guidSqlPkg), 
        EnumTypes = new Type[] {}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => CustomGroupGuids.guidUnknown1), 
        EnumTypes = new Type[] {}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => CustomGroupGuids.guidBrowserLinkCmdSet), 
        EnumTypes = new Type[] {}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => CustomGroupGuids.guidUnknown2), 
        EnumTypes = new Type[] {}
      },
      new MenuGroupDefinition {
        MemberInfo = ReflectionUtils.GetMemberInfo(() => CustomGroupGuids.guidVenusCmdId), 
        EnumTypes = new Type[] {}
      },
    };

    private static readonly HashSet<Guid> MenuGroupsToSkip = new HashSet<Guid>() {
      CustomGroupGuids.guidResharperPkg,
      CustomGroupGuids.guidFSharpProjectCmdSet,
      CustomGroupGuids.guidSqlPkg,
      CustomGroupGuids.guidUnknown1,
      CustomGroupGuids.guidUnknown2,
      CustomGroupGuids.guidBrowserLinkCmdSet,
      CustomGroupGuids.guidVenusCmdId,
    };

    public VsHierarchyLogger(VsHierarchy vsHierarchy) {
      _vsHierarchy = vsHierarchy;
      //Enabled = true;
      //LogPropertyIdActivity = true;
      //LogPropteryGuidActivity = true;
      //LogCommandTargetActivity = true;
    }

    public bool Enabled { get; set; }
    public bool LogPropertyIdActivity { get; set; }
    public bool LogPropteryGuidActivity { get; set; }
    public bool LogCommandTargetActivity { get; set; }

    public void Log(string format, params object[] args) {
      if (!Enabled)
        return;

      Logger.LogInfo("VsHierarchy: {0}", string.Format(format, args));
    }

    public void LogProperty(string message, uint itemid, int propid) {
      if (!LogPropertyIdActivity)
        return;

      Log("{0}({1}) - {2}", message, unchecked((int)itemid), GetEnumName(propid, VshPropTypes));
    }

    public void LogPropertyGuid(string message, uint itemid, int propid) {
      if (!LogPropteryGuidActivity)
        return;

      Log("{0}({1}) - {2}", message, unchecked((int)itemid), GetEnumName(propid, VshPropTypes));
    }

    public void LogExecCommand(uint itemid, Guid pguidCmdGroup, uint nCmdId, uint nCmdexecopt) {
      if (!LogCommandTargetActivity)
        return;

      if (MenuGroupsToSkip.Contains(pguidCmdGroup))
        return;

      Log("Exec({0}): {1}", (int)itemid, GetCommandName(pguidCmdGroup, nCmdId));
    }

    public void LogQueryStatusCommand(uint itemid, Guid pguidCmdGroup, uint cmdId) {
      if (!LogCommandTargetActivity)
        return;

      if (MenuGroupsToSkip.Contains(pguidCmdGroup))
        return;

      Log("QueryStatus({0}): {1}", (int)itemid, GetCommandName(pguidCmdGroup, cmdId));
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
