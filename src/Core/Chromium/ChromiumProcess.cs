// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Processes;
using VsChromium.Core.Utility;
using VsChromium.Core.Win32.Processes;
using VsChromium.Core.Win32.Shell;

namespace VsChromium.Core.Chromium {
  public class ChromiumProcess {
    private InstallationData _installationData;
    private ProcessCategory _category;
    private string[] _commandLine;
    private NtProcess _ntProcess;

    public static ChromiumProcess Create(int pid) {
      InstallationData data = InstallationData.Create(pid);
      if (data == null)
        return null;
      return new ChromiumProcess(pid, data);
    }

    public ChromiumProcess(int pid, InstallationData installationData) {
      _ntProcess = new NtProcess(pid);
      _installationData = installationData;
      _category = ProcessCategory.Unknown;
      _commandLine = null;
    }

    public IList<string> CommandLineArgs {
      get { return _commandLine; }
    }

    public int Pid {
      get { return _ntProcess.ProcessId; }
    }

    public int ParentPid {
      get { return _ntProcess.ParentProcessId; }
    }

    public string ExecutablePath {
      get { return _ntProcess.Win32ProcessImagePath; }
    }

    public InstallationData InstallationData {
      get {
        return _installationData;
      }
    }

    public Icon Icon {
      get {
        ushort iconIndex = (ushort)_installationData.IconIndex;
        IntPtr hicon = Core.Win32.Shell.NativeMethods.ExtractAssociatedIcon(IntPtr.Zero, ExecutablePath, ref iconIndex);
        if (hicon == IntPtr.Zero) {
          SHFileInfo info = new SHFileInfo(true);
          SHGFI flags = SHGFI.Icon | SHGFI.SmallIcon | SHGFI.OpenIcon | SHGFI.UseFileAttributes;
          int cbFileInfo = Marshal.SizeOf(info);
          VsChromium.Core.Win32.Shell.NativeMethods.SHGetFileInfo(
            ExecutablePath, 256, ref info, (uint)cbFileInfo, (uint)flags);
          hicon = info.hIcon;
        }
        return (hicon == IntPtr.Zero) ? null : Icon.FromHandle(hicon);
      }
    }

    public ProcessCategory Category {
      get {
        if (_category != ProcessCategory.Unknown)
          return _category;

        _commandLine = ChromeUtility.SplitArgs(_ntProcess.CommandLine);
        if (_commandLine == null || _commandLine.Length == 0)
          return ProcessCategory.Other;

        string file = Path.GetFileName(ExecutablePath);
        if (file.Equals("delegate_execute.exe", StringComparison.CurrentCultureIgnoreCase))
          return ProcessCategory.DelegateExecute;
        else if (file.Equals("chrome.exe", StringComparison.CurrentCultureIgnoreCase)) {
          if (_commandLine.Contains("--type=renderer"))
            return ProcessCategory.Renderer;
          else if (_commandLine.Contains("--type=plugin"))
            return ProcessCategory.Plugin;
          else if (_commandLine.Contains("--type=ppapi"))
            return ProcessCategory.Ppapi;
          else if (_commandLine.Contains("--type=gpu-process"))
            return ProcessCategory.Gpu;
          else if (_commandLine.Contains("--type=service"))
            return ProcessCategory.Service;
          else if (_commandLine.Contains("--type=ppapi-broker"))
            return ProcessCategory.PpapiBroker;
          else if (_commandLine.Any(arg => arg.StartsWith("-ServerName")))
            return ProcessCategory.MetroViewer;
          else
            return ProcessCategory.Browser;
        } else
          return ProcessCategory.Other;
      }
    }
  }
}
