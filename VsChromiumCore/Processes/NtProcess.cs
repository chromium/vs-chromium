// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.Win32.SafeHandles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;

using VsChromiumCore.Utility;
using VsChromiumCore.Win32;
using VsChromiumCore.Win32.Files;
using VsChromiumCore.Win32.Processes;
using System.IO;

namespace VsChromiumCore.Processes {
  public class NtProcess : IDisposable {
    public NtProcess(int pid) {
      // Initialize everything to null in case something fails.
      _processId = pid;
      _processHandleFlags = ProcessAccessFlags.None;
      _cachedProcessBasicInfo = null;
      _machineTypeIsLoaded = false;
      _machineType = MachineType.Unknown;
      _cachedPeb = null;
      _cachedProcessParams = null;
      _cachedCommandLine = null;
      _processHandle = new SafeProcessHandle();

      OpenAndCacheProcessHandle();
    }

    // Returns the machine type (x86, x64, etc) of this process.  Uses lazy evaluation and caches
    // the result.
    public MachineType MachineType {
      get {
        if (_machineTypeIsLoaded)
          return _machineType;
        if (!CanQueryProcessInformation)
          return MachineType.Unknown;

        CacheMachineType();
        return _machineType;
      }
    }

    public string NativeProcessImagePath {
      get {
        if (_nativeProcessImagePath == null) {
          _nativeProcessImagePath = QueryProcessImageName(
            ProcessQueryImageNameMode.NativeSystemFormat);
        }
        return _nativeProcessImagePath;
      }
    }

    public int ParentProcessId {
      get {
        if (_cachedProcessBasicInfo == null) {
          CacheProcessInformation();

          return _cachedProcessBasicInfo.Value.ParentProcessId.ToInt32();
        }
        return -1;
      }
    }

    public string Win32ProcessImagePath {
      get {
        if (_win32ProcessImagePath == null) {
          _win32ProcessImagePath = QueryProcessImageName(
            ProcessQueryImageNameMode.Win32);
        }
        return _win32ProcessImagePath;
      }
    }

    // Returns the command line that this process was launched with.  Uses lazy evaluation and
    // caches the result.  Reads the command line from the PEB of the running process.
    public string CommandLine {
      get {
        if (!CanReadPeb)
          throw new InvalidOperationException();
        CacheProcessInformation();
        CachePeb();
        CacheProcessParams();
        CacheCommandLine();
        return _cachedCommandLine;
      }
    }

    // Determines if we have permission to read the process's PEB.
    public bool CanReadPeb {
      get {
        ProcessAccessFlags required_flags = ProcessAccessFlags.VmRead | ProcessAccessFlags.QueryInformation;

        // In order to read the PEB, we must have *both* of these flags.
        if ((_processHandleFlags & required_flags) != required_flags)
          return false;

        // If we're on a 64-bit OS, in a 32-bit process, and the target process is not 32-bit,
        // we can't read its PEB.
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
            && (MachineType != MachineType.X86))
          return false;

        return true;
      }
    }

    // If we can't read the process's PEB, we may still be able to get other kinds of information
    // from the process.  This flag determines if we can get lesser information.
    private bool CanQueryProcessInformation {
      get {
        ProcessAccessFlags required_flags = ProcessAccessFlags.QueryLimitedInformation | ProcessAccessFlags.QueryInformation;

        // In order to query the process, we need *either* of these flags.
        return (_processHandleFlags & required_flags) != ProcessAccessFlags.None;
      }
    }

    private string QueryProcessImageName(ProcessQueryImageNameMode mode) {
      StringBuilder moduleBuffer = new StringBuilder(1024);
      int size = moduleBuffer.Capacity;
      VsChromiumCore.Win32.Processes.NativeMethods.QueryFullProcessImageName(
        _processHandle, mode, moduleBuffer, ref size);
      if (mode == ProcessQueryImageNameMode.NativeSystemFormat)
        moduleBuffer.Insert(0, "\\\\?\\GLOBALROOT");
      return moduleBuffer.ToString();
    }

    // Loads the top-level structure of the process's information block and caches it.
    private void CacheProcessInformation() {
      System.Diagnostics.Debug.Assert(CanReadPeb);

      // Fetch the process info and set the fields.
      ProcessBasicInformation temp = new ProcessBasicInformation();
      int size;
      int status = VsChromiumCore.Win32.Processes.NativeMethods.NtQueryInformationProcess(
        _processHandle,
        ProcessInfoClass.BasicInformation,
        ref temp,
        MarshalUtility.UnmanagedStructSize<ProcessBasicInformation>(),
        out size);

      if (status != NtStatus.Success) {
        throw new Win32Exception();
      }

      _cachedProcessBasicInfo = temp;
    }

    // Follows a pointer from the PROCESS_BASIC_INFORMATION structure in the target process's
    // address space to read the PEB.
    private void CachePeb() {
      System.Diagnostics.Debug.Assert(CanReadPeb);

      if (_cachedPeb == null) {
        _cachedPeb = MarshalUtility.ReadUnmanagedStructFromProcess<Peb>(
          _processHandle,
          _cachedProcessBasicInfo.Value.PebBaseAddress);
      }
    }

    // Follows a pointer from the PEB structure in the target process's address space to read the
    // RTL_USER_PROCESS_PARAMETERS structure.
    private void CacheProcessParams() {
      System.Diagnostics.Debug.Assert(CanReadPeb);

      if (_cachedProcessParams == null) {
        _cachedProcessParams =
          MarshalUtility.ReadUnmanagedStructFromProcess<RtlUserProcessParameters>(
            _processHandle, _cachedPeb.Value.ProcessParameters);
      }
    }

    private void CacheCommandLine() {
      System.Diagnostics.Debug.Assert(CanReadPeb);

      if (_cachedCommandLine == null) {
        _cachedCommandLine = MarshalUtility.ReadStringUniFromProcess(
          _processHandle,
          _cachedProcessParams.Value.CommandLine.Buffer,
          _cachedProcessParams.Value.CommandLine.Length / 2);
      }
    }

    private void CacheMachineType() {
      System.Diagnostics.Debug.Assert(CanQueryProcessInformation);

      // If our extension is running in a 32-bit process (which it is), then attempts to access
      // files in C:\windows\system (and a few other files) will redirect to C:\Windows\SysWOW64
      // and we will mistakenly think that the image file is a 32-bit image.  The way around this
      // is to use a native system format path, of the form:
      //    \\?\GLOBALROOT\Device\HarddiskVolume0\Windows\System\foo.dat
      // NativeProcessImagePath gives us the full process image path in the desired format.
      string path = NativeProcessImagePath;

      // Open the PE File as a binary file, and parse just enough information to determine the
      // machine type.
      //http://www.microsoft.com/whdc/system/platform/firmware/PECOFF.mspx
      using (SafeFileHandle safeHandle =
                Win32.Files.NativeMethods.CreateFile(
                    path,
                    NativeAccessFlags.GenericRead,
                    FileShare.Read,
                    IntPtr.Zero,
                    FileMode.Open,
                    FileAttributes.Normal,
                    IntPtr.Zero)) {
        FileStream fs = new FileStream(safeHandle, FileAccess.Read);
        using (BinaryReader br = new BinaryReader(fs)) {
          fs.Seek(0x3c, SeekOrigin.Begin);
          Int32 peOffset = br.ReadInt32();
          fs.Seek(peOffset, SeekOrigin.Begin);
          UInt32 peHead = br.ReadUInt32();
          if (peHead != 0x00004550) // "PE\0\0", little-endian
            throw new Exception("Can't find PE header");
          _machineType = (MachineType)br.ReadUInt16();
          _machineTypeIsLoaded = true;
        }
      }
    }

    private void OpenAndCacheProcessHandle() {
      // Try to open a handle to the process with the highest level of privilege, but if we can't
      // do that then fallback to requesting access with a lower privilege level.
      _processHandleFlags = ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VmRead;
      _processHandle = Win32.Processes.NativeMethods.OpenProcess(_processHandleFlags, false, _processId);
      if (_processHandle.IsInvalid) {
        _processHandleFlags = ProcessAccessFlags.QueryLimitedInformation;
        _processHandle = Win32.Processes.NativeMethods.OpenProcess(_processHandleFlags, false, _processId);
        if (_processHandle.IsInvalid) {
          _processHandleFlags = ProcessAccessFlags.None;
          throw new Win32Exception();
        }
      }
    }

    // An open handle to the process, along with the set of access flags that the handle was
    // open with.
    private readonly int _processId;
    private SafeProcessHandle _processHandle;
    private ProcessAccessFlags _processHandleFlags;
    private string _nativeProcessImagePath;
    private string _win32ProcessImagePath;

    // The machine type is read by parsing the PE image file of the running process, so we cache
    // its value since the operation expensive.
    private bool _machineTypeIsLoaded;
    private MachineType _machineType;

    // The following fields exist ultimately so that we can access the command line.  However,
    // each field must be read separately through a pointer into another process's address
    // space so the access is expensive, hence we cache the values.
    private Nullable<ProcessBasicInformation> _cachedProcessBasicInfo;
    private Nullable<Peb> _cachedPeb;
    private Nullable<RtlUserProcessParameters> _cachedProcessParams;
    private string _cachedCommandLine;

    public void Dispose() {
      _processHandle.Dispose();
    }
  }
}
