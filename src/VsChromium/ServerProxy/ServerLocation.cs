// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// Location of the indexing server executable in the directory of the
  /// package assemblies:
  /// <list type="bullet">
  /// <item><c>Server\win-x64\VsChromium.Server.exe</c> and
  /// <c>Server\win-arm64\VsChromium.Server.exe</c>: the self-contained servers
  /// of the VSIX (Release builds), one per architecture of Windows. The server
  /// of the native architecture of the OS is used, even when Visual Studio
  /// itself runs as an x64 process emulated on ARM64 Windows: the server is
  /// CPU and memory intensive, the native build is much faster.</item>
  /// <item><c>Server\VsChromium.Server.exe</c>: the framework-dependent build
  /// of the server (Debug builds, e.g. F5 in the experimental instance).</item>
  /// </list>
  /// </summary>
  public static class ServerLocation {
    public const ushort ImageFileMachineUnknown = 0;
    public const ushort ImageFileMachineI386 = 0x014c;
    public const ushort ImageFileMachineAmd64 = 0x8664;
    public const ushort ImageFileMachineArm64 = 0xAA64;

    public const string ServerDirectoryName = "Server";
    public const string ServerExecutableName = "VsChromium.Server.exe";
    public const string WinX64 = "win-x64";
    public const string WinArm64 = "win-arm64";

    /// <summary>
    /// The runtime identifier of the server for the native architecture of
    /// the OS (<paramref name="nativeMachine"/>, an IMAGE_FILE_MACHINE_xxx
    /// value as returned by IsWow64Process2), or, if it is unknown
    /// (<see cref="ImageFileMachineUnknown"/>, e.g. IsWow64Process2 is not
    /// available), of <paramref name="osArchitecture"/>. Returns null for
    /// architectures without a server.
    /// </summary>
    public static string GetRuntimeIdentifier(ushort nativeMachine, Architecture osArchitecture) {
      switch (nativeMachine) {
        case ImageFileMachineArm64:
          return WinArm64;
        case ImageFileMachineAmd64:
          return WinX64;
        case ImageFileMachineUnknown:
          break;
        default:
          return null;
      }
      switch (osArchitecture) {
        case Architecture.Arm64:
          return WinArm64;
        case Architecture.X64:
          return WinX64;
        default:
          return null;
      }
    }

    /// <summary>
    /// The paths of the server executable to try, relative to the directory
    /// of the package assemblies, in order of preference, for the runtime
    /// identifier <paramref name="runtimeIdentifier"/> (null if unknown).
    /// </summary>
    public static IList<string> GetCandidatePaths(string runtimeIdentifier) {
      var result = new List<string>();
      if (runtimeIdentifier != null) {
        result.Add(Path.Combine(ServerDirectoryName, runtimeIdentifier, ServerExecutableName));
      }
      // ARM64 Windows runs x64 executables (emulated), not the reverse.
      if (runtimeIdentifier == WinArm64) {
        result.Add(Path.Combine(ServerDirectoryName, WinX64, ServerExecutableName));
      }
      result.Add(Path.Combine(ServerDirectoryName, ServerExecutableName));
      return result;
    }

    /// <summary>
    /// The full path of the first existing candidate (see <see
    /// cref="GetCandidatePaths"/>) in <paramref name="packageDirectory"/>, or
    /// null if none exists.
    /// </summary>
    public static string FindServerPath(string packageDirectory, string runtimeIdentifier, Func<string, bool> fileExists) {
      return GetCandidatePaths(runtimeIdentifier)
        .Select(x => Path.Combine(packageDirectory, x))
        .FirstOrDefault(fileExists);
    }

    /// <summary>
    /// The runtime identifier of the server for the native architecture of
    /// this machine (see <see cref="GetRuntimeIdentifier"/>).
    /// </summary>
    public static string GetNativeRuntimeIdentifier() {
      return GetRuntimeIdentifier(GetNativeMachine(), RuntimeInformation.OSArchitecture);
    }

    /// <summary>
    /// The native architecture of the OS (IMAGE_FILE_MACHINE_xxx), not the
    /// architecture of this process (which may be emulated), or <see
    /// cref="ImageFileMachineUnknown"/> if IsWow64Process2 is not available
    /// (Windows 10 before version 1709).
    /// </summary>
    public static ushort GetNativeMachine() {
      try {
        ushort processMachine;
        ushort nativeMachine;
        if (IsWow64Process2(GetCurrentProcess(), out processMachine, out nativeMachine)) {
          return nativeMachine;
        }
      } catch (EntryPointNotFoundException) {
      } catch (DllNotFoundException) {
      }
      return ImageFileMachineUnknown;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);
  }
}
