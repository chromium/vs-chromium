// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using VsChromium.Core.Utility;
using VsChromium.Core.Win32;
using VsChromium.Core.Win32.Processes;

namespace VsChromium.Core.Processes {
  public class NtProcess {
    public NtProcess(int pid) {
      _processId = pid;
      _isBeingDebugged = false;
      _machineType = MachineType.Unknown;
      _commandLine = null;
      _nativeProcessImagePath = null;
      _win32ProcessImagePath = null;
      _isValid = false;

      LoadProcessInfo();
    }

    private void LoadProcessInfo() {
      ProcessAccessFlags flags;
      try {
        _isValid = false;
        using (SafeProcessHandle handle = OpenProcessHandle(out flags)) {
          if (handle.IsInvalid)
            return;

          _nativeProcessImagePath = QueryProcessImageName(
              handle,
              ProcessQueryImageNameMode.NativeSystemFormat);
          _win32ProcessImagePath = QueryProcessImageName(
              handle, 
              ProcessQueryImageNameMode.Win32);

          // If our extension is running in a 32-bit process (which it is), then attempts to access
          // files in C:\windows\system (and a few other files) will redirect to
          // C:\Windows\SysWOW64 and we will mistakenly think that the image file is a 32-bit
          // image.  The way around this is to use a native system format path, of the form:
          //    \\?\GLOBALROOT\Device\HarddiskVolume0\Windows\System\foo.dat
          // NativeProcessImagePath gives us the full process image path in the desired format.
          _machineType = ProcessUtility.GetMachineType(NativeProcessImagePath);

          MachineType hostProcessArch = (IntPtr.Size == 4) ? MachineType.X86 : MachineType.X64;
          // If the extension is 32-bit and the target process is 64-bit, we have to use Wow64-
          // specific functions to read the memory of the target process.
          _isValid = (_machineType == hostProcessArch) 
              ? LoadProcessInfoNative(handle, flags)
              : LoadProcessInfoWow64(handle, flags);
        }
      } catch (Exception) {
        _isValid = false;
      }
    }

    // Reads native process info from a 64/32-bit process in the case where the target architecture
    // of this process is the same as that of the target process.
    private bool LoadProcessInfoNative(SafeProcessHandle handle, ProcessAccessFlags flags) {
      ProcessBasicInformation basicInfo = new ProcessBasicInformation();
      int size;
      int status = NativeMethods.NtQueryInformationProcess(
          handle,
          ProcessInfoClass.BasicInformation,
          ref basicInfo,
          MarshalUtility.UnmanagedStructSize<ProcessBasicInformation>(),
          out size);
      _parentProcessId = basicInfo.ParentProcessId.ToInt32();

      // If we can't load the ProcessBasicInfo, then we can't really do anything.
      if (status != NtStatus.Success || basicInfo.PebBaseAddress == IntPtr.Zero)
        return false;

      if (flags.HasFlag(ProcessAccessFlags.VmRead)) {
        // Follows a pointer from the PROCESS_BASIC_INFORMATION structure in the target process's
        // address space to read the PEB.
        Peb peb = MarshalUtility.ReadUnmanagedStructFromProcess<Peb>(
            handle,
            basicInfo.PebBaseAddress);

        _isBeingDebugged = peb.IsBeingDebugged;

        if (peb.ProcessParameters != IntPtr.Zero) {
          // Follows a pointer from the PEB structure in the target process's address space to read
          // the RTL_USER_PROCESS_PARAMS.
          RtlUserProcessParameters processParameters = new RtlUserProcessParameters();
          processParameters = MarshalUtility.ReadUnmanagedStructFromProcess<RtlUserProcessParameters>(
              handle,
              peb.ProcessParameters);

          _commandLine = MarshalUtility.ReadStringUniFromProcess(
              handle,
              processParameters.CommandLine.Buffer,
              processParameters.CommandLine.Length / 2);
        }
      }
      return true;
    }

    // Reads native process info from a 64-bit process in the case where this function is executing
    // in a 32-bit process.
    private bool LoadProcessInfoWow64(SafeProcessHandle handle, ProcessAccessFlags flags) {
      ulong pebSize = (ulong)MarshalUtility.UnmanagedStructSize<PebWow64>();
      ulong processParamsSize = 
          (ulong)MarshalUtility.UnmanagedStructSize<RtlUserProcessParametersWow64>();

      // Read PROCESS_BASIC_INFORMATION up to and including the pointer to PEB structure.
      int processInfoSize =
          MarshalUtility.UnmanagedStructSize<ProcessBasicInformationWow64>();
      ProcessBasicInformationWow64 pbi = new ProcessBasicInformationWow64();
      int result = NativeMethods.NtWow64QueryInformationProcess64(
          handle,
          ProcessInfoClass.BasicInformation,
          ref pbi,
          processInfoSize,
          out processInfoSize);
      if (result != 0)
        return false;

      _parentProcessId = (int)pbi.ParentProcessId;
      Debug.Assert((int)pbi.UniqueProcessId == _processId);

      if (flags.HasFlag(ProcessAccessFlags.VmRead)) {
        IntPtr pebBuffer = IntPtr.Zero;
        IntPtr processParametersBuffer = IntPtr.Zero;
        IntPtr commandLineBuffer = IntPtr.Zero;

        try {
          pebBuffer = Marshal.AllocHGlobal((int)pebSize);
          // Read PEB up to and including the pointer to RTL_USER_PROCESS_PARAMETERS
          // structure.
          result = NativeMethods.NtWow64ReadVirtualMemory64(
              handle,
              pbi.PebBaseAddress, 
              pebBuffer, 
              pebSize, 
              out pebSize);
          if (result != 0)
            return false;
          PebWow64 peb = (PebWow64)Marshal.PtrToStructure(pebBuffer, typeof(PebWow64));
          _isBeingDebugged = peb.IsBeingDebugged;

          processParametersBuffer = Marshal.AllocHGlobal((int)processParamsSize);
          result = NativeMethods.NtWow64ReadVirtualMemory64(
              handle,
              peb.ProcessParameters,
              processParametersBuffer,
              processParamsSize,
              out processParamsSize);
          if (result != 0)
            return false;
          RtlUserProcessParametersWow64 processParameters = (RtlUserProcessParametersWow64)
              Marshal.PtrToStructure(
                  processParametersBuffer, 
                  typeof(RtlUserProcessParametersWow64));

          ulong commandLineBufferSize = (ulong)processParameters.CommandLine.MaximumLength;
          commandLineBuffer = Marshal.AllocHGlobal((int)commandLineBufferSize);
          result = NativeMethods.NtWow64ReadVirtualMemory64(
              handle,
              processParameters.CommandLine.Buffer,
              commandLineBuffer,
              commandLineBufferSize,
              out commandLineBufferSize);
          if (result != 0)
            return false;
          _commandLine = Marshal.PtrToStringUni(commandLineBuffer);
        } finally {
          if (pebBuffer != IntPtr.Zero)
            Marshal.FreeHGlobal(pebBuffer);
          if (commandLineBuffer != IntPtr.Zero)
            Marshal.FreeHGlobal(commandLineBuffer);
          if (processParametersBuffer != IntPtr.Zero)
            Marshal.FreeHGlobal(processParametersBuffer);
        }
      }
      return true;
    }

    private SafeProcessHandle OpenProcessHandle(out ProcessAccessFlags flags) {
      // Try to open a handle to the process with the highest level of privilege, but if we can't
      // do that then fallback to requesting access with a lower privilege level.
      flags = ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VmRead;
      SafeProcessHandle handle;
      handle = NativeMethods.OpenProcess(flags, false, _processId);
      if (!handle.IsInvalid)
        return handle;

      flags = ProcessAccessFlags.QueryLimitedInformation;
      handle = NativeMethods.OpenProcess(flags, false, _processId);
      if (handle.IsInvalid)
        flags = ProcessAccessFlags.None;
      return handle;
    }

    public MachineType MachineType {
      get { return _machineType; }
    }

    public int ProcessId {
      get { return _processId; }
    }

    public int ParentProcessId {
      get { return _parentProcessId; }
    }

    public string NativeProcessImagePath {
      get { return _nativeProcessImagePath; }
    }

    public string Win32ProcessImagePath {
      get { return _win32ProcessImagePath; }
    }

    public string CommandLine {
      get { return _commandLine; }
    }

    public bool IsBeingDebugged {
      get { return _isBeingDebugged; }
    }

    public bool IsValid {
      get { return _isValid; }
    }

    private string QueryProcessImageName(SafeProcessHandle handle, ProcessQueryImageNameMode mode) {
      StringBuilder moduleBuffer = new StringBuilder(1024);
      int size = moduleBuffer.Capacity;
      NativeMethods.QueryFullProcessImageName(
        handle, mode, moduleBuffer, ref size);
      if (mode == ProcessQueryImageNameMode.NativeSystemFormat)
        moduleBuffer.Insert(0, "\\\\?\\GLOBALROOT");
      return moduleBuffer.ToString();
    }

    private readonly int _processId;
    private bool _isValid;
    private bool _isBeingDebugged;
    private int _parentProcessId;
    private string _nativeProcessImagePath;
    private string _win32ProcessImagePath;
    private MachineType _machineType;
    private string _commandLine;
  }
}
