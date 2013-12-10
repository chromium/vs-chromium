// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VsChromiumPackage.ChromeDebug.LowLevel;

namespace VsChromiumPackage.ChromeDebug {
  class ProcessDetail : IDisposable {
    public ProcessDetail(int pid) {
      // Initialize everything to null in case something fails.
      _processId = pid;
      _processHandleFlags = LowLevelTypes.ProcessAccessFlags.NONE;
      _cachedProcessBasicInfo = null;
      _machineTypeIsLoaded = false;
      _machineType = LowLevelTypes.MachineType.UNKNOWN;
      _cachedPeb = null;
      _cachedProcessParams = null;
      _cachedCommandLine = null;
      _processHandle = IntPtr.Zero;

      OpenAndCacheProcessHandle();
    }

    // Returns the machine type (x86, x64, etc) of this process.  Uses lazy evaluation and caches
    // the result.
    public LowLevelTypes.MachineType MachineType {
      get {
        if (_machineTypeIsLoaded)
          return _machineType;
        if (!CanQueryProcessInformation)
          return LowLevelTypes.MachineType.UNKNOWN;

        CacheMachineType();
        return _machineType;
      }
    }

    public string NativeProcessImagePath {
      get {
        if (_nativeProcessImagePath == null) {
          _nativeProcessImagePath = QueryProcessImageName(
            LowLevelTypes.ProcessQueryImageNameMode.NATIVE_SYSTEM_FORMAT);
        }
        return _nativeProcessImagePath;
      }
    }

    public string Win32ProcessImagePath {
      get {
        if (_win32ProcessImagePath == null) {
          _win32ProcessImagePath = QueryProcessImageName(
            LowLevelTypes.ProcessQueryImageNameMode.WIN32_FORMAT);
        }
        return _win32ProcessImagePath;
      }
    }

    public Icon SmallIcon {
      get {
        LowLevel.LowLevelTypes.SHFILEINFO info = new LowLevelTypes.SHFILEINFO(true);
        LowLevel.LowLevelTypes.SHGFI flags = LowLevel.LowLevelTypes.SHGFI.Icon
                                             | LowLevelTypes.SHGFI.SmallIcon
                                             | LowLevelTypes.SHGFI.OpenIcon
                                             | LowLevelTypes.SHGFI.UseFileAttributes;
        int cbFileInfo = Marshal.SizeOf(info);
        LowLevel.NativeMethods.SHGetFileInfo(Win32ProcessImagePath,
                                             256,
                                             ref info,
                                             (uint)cbFileInfo,
                                             (uint)flags);
        return Icon.FromHandle(info.hIcon);
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
        LowLevelTypes.ProcessAccessFlags required_flags =
          LowLevelTypes.ProcessAccessFlags.VM_READ
          | LowLevelTypes.ProcessAccessFlags.QUERY_INFORMATION;

        // In order to read the PEB, we must have *both* of these flags.
        if ((_processHandleFlags & required_flags) != required_flags)
          return false;

        // If we're on a 64-bit OS, in a 32-bit process, and the target process is not 32-bit,
        // we can't read its PEB.
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
            && (MachineType != LowLevelTypes.MachineType.X86))
          return false;

        return true;
      }
    }

    // If we can't read the process's PEB, we may still be able to get other kinds of information
    // from the process.  This flag determines if we can get lesser information.
    private bool CanQueryProcessInformation {
      get {
        LowLevelTypes.ProcessAccessFlags required_flags =
          LowLevelTypes.ProcessAccessFlags.QUERY_LIMITED_INFORMATION
          | LowLevelTypes.ProcessAccessFlags.QUERY_INFORMATION;

        // In order to query the process, we need *either* of these flags.
        return (_processHandleFlags & required_flags) != LowLevelTypes.ProcessAccessFlags.NONE;
      }
    }

    private string QueryProcessImageName(LowLevelTypes.ProcessQueryImageNameMode mode) {
      StringBuilder moduleBuffer = new StringBuilder(1024);
      int size = moduleBuffer.Capacity;
      NativeMethods.QueryFullProcessImageName(
        _processHandle,
        mode,
        moduleBuffer,
        ref size);
      if (mode == LowLevelTypes.ProcessQueryImageNameMode.NATIVE_SYSTEM_FORMAT)
        moduleBuffer.Insert(0, "\\\\?\\GLOBALROOT");
      return moduleBuffer.ToString();
    }

    // Loads the top-level structure of the process's information block and caches it.
    private void CacheProcessInformation() {
      System.Diagnostics.Debug.Assert(CanReadPeb);

      // Fetch the process info and set the fields.
      LowLevelTypes.PROCESS_BASIC_INFORMATION temp = new LowLevelTypes.PROCESS_BASIC_INFORMATION();
      int size;
      LowLevelTypes.NTSTATUS status = NativeMethods.NtQueryInformationProcess(
        _processHandle,
        LowLevelTypes.PROCESSINFOCLASS.PROCESS_BASIC_INFORMATION,
        ref temp,
        Utility.UnmanagedStructSize<LowLevelTypes.PROCESS_BASIC_INFORMATION>(),
        out size);

      if (status != LowLevelTypes.NTSTATUS.SUCCESS) {
        throw new Win32Exception();
      }

      _cachedProcessBasicInfo = temp;
    }

    // Follows a pointer from the PROCESS_BASIC_INFORMATION structure in the target process's
    // address space to read the PEB.
    private void CachePeb() {
      System.Diagnostics.Debug.Assert(CanReadPeb);

      if (_cachedPeb == null) {
        _cachedPeb = Utility.ReadUnmanagedStructFromProcess<LowLevelTypes.PEB>(
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
          Utility.ReadUnmanagedStructFromProcess<LowLevelTypes.RTL_USER_PROCESS_PARAMETERS>(
            _processHandle, _cachedPeb.Value.ProcessParameters);
      }
    }

    private void CacheCommandLine() {
      System.Diagnostics.Debug.Assert(CanReadPeb);

      if (_cachedCommandLine == null) {
        _cachedCommandLine = Utility.ReadStringUniFromProcess(
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
      using (SafeFileHandle safeHandle = NativeMethods.CreateFile(
        path,
        LowLevelTypes.FileAccessFlags.GENERIC_READ,
        LowLevelTypes.FileShareFlags.SHARE_READ,
        IntPtr.Zero,
        LowLevelTypes.FileCreationDisposition.OPEN_EXISTING,
        LowLevelTypes.FileFlagsAndAttributes.NORMAL,
        IntPtr.Zero)) {
        FileStream fs = new FileStream(safeHandle, FileAccess.Read);
        using (BinaryReader br = new BinaryReader(fs)) {
          fs.Seek(0x3c, SeekOrigin.Begin);
          Int32 peOffset = br.ReadInt32();
          fs.Seek(peOffset, SeekOrigin.Begin);
          UInt32 peHead = br.ReadUInt32();
          if (peHead != 0x00004550) // "PE\0\0", little-endian
            throw new Exception("Can't find PE header");
          _machineType = (LowLevelTypes.MachineType)br.ReadUInt16();
          _machineTypeIsLoaded = true;
        }
      }
    }

    private void OpenAndCacheProcessHandle() {
      // Try to open a handle to the process with the highest level of privilege, but if we can't
      // do that then fallback to requesting access with a lower privilege level.
      _processHandleFlags = LowLevelTypes.ProcessAccessFlags.QUERY_INFORMATION
                           | LowLevelTypes.ProcessAccessFlags.VM_READ;
      _processHandle = NativeMethods.OpenProcess(_processHandleFlags, false, _processId);
      if (_processHandle == IntPtr.Zero) {
        _processHandleFlags = LowLevelTypes.ProcessAccessFlags.QUERY_LIMITED_INFORMATION;
        _processHandle = NativeMethods.OpenProcess(_processHandleFlags, false, _processId);
        if (_processHandle == IntPtr.Zero) {
          _processHandleFlags = LowLevelTypes.ProcessAccessFlags.NONE;
          throw new Win32Exception();
        }
      }
    }

    // An open handle to the process, along with the set of access flags that the handle was
    // open with.
    private readonly int _processId;
    private IntPtr _processHandle;
    private LowLevelTypes.ProcessAccessFlags _processHandleFlags;
    private string _nativeProcessImagePath;
    private string _win32ProcessImagePath;

    // The machine type is read by parsing the PE image file of the running process, so we cache
    // its value since the operation expensive.
    private bool _machineTypeIsLoaded;
    private LowLevelTypes.MachineType _machineType;

    // The following fields exist ultimately so that we can access the command line.  However,
    // each field must be read separately through a pointer into another process's address
    // space so the access is expensive, hence we cache the values.
    private Nullable<LowLevelTypes.PROCESS_BASIC_INFORMATION> _cachedProcessBasicInfo;
    private Nullable<LowLevelTypes.PEB> _cachedPeb;
    private Nullable<LowLevelTypes.RTL_USER_PROCESS_PARAMETERS> _cachedProcessParams;
    private string _cachedCommandLine;

    ~ProcessDetail() {
      Dispose();
    }

    public void Dispose() {
      if (_processHandle != IntPtr.Zero)
        NativeMethods.CloseHandle(_processHandle);
      _processHandle = IntPtr.Zero;
    }
  }
}
