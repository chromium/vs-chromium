// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VsChromium.Bench.Platform;

/// <summary>
/// Flushes the OS file cache before the "cold" measurements. Needs admin/root, and is only done when
/// the user agreed (interactively or with --flush-cache yes):
/// <list type="bullet">
/// <item>Windows: flush of the modified page list, then purge of the standby list
/// (NtSetSystemInformation(SystemMemoryListInformation), with SeProfileSingleProcessPrivilege).</item>
/// <item>Linux: sync, then "echo 3 &gt; /proc/sys/vm/drop_caches".</item>
/// <item>macOS: purge.</item>
/// </list>
/// </summary>
public static class CacheFlusher {
  /// <summary>Null if the cache can be flushed on this machine (elevated), otherwise the reason.</summary>
  public static string? Unavailable(bool elevated) =>
    elevated ? null : OperatingSystem.IsWindows() ? "flushing the OS file cache needs admin" : "flushing the OS file cache needs root";

  /// <summary>Flushes the cache; returns null on success, otherwise the error.</summary>
  public static string? Flush() {
    if (OperatingSystem.IsWindows()) {
      return FlushWindows();
    }
    if (OperatingSystem.IsLinux()) {
      ProcessRunner.Run("sync", []);
      try {
        File.WriteAllText("/proc/sys/vm/drop_caches", "3\n");
        return null;
      } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        return e.Message;
      }
    }
    if (OperatingSystem.IsMacOS()) {
      ProcessRunner.Run("sync", []);
      var result = ProcessRunner.Run("purge", [], TimeSpan.FromMinutes(2));
      return result is { Success: true } ? null : "purge failed";
    }
    return "not supported on this OS";
  }

  [SupportedOSPlatform("windows")]
  private static unsafe string? FlushWindows() {
    if (!EnablePrivilege("SeProfileSingleProcessPrivilege")) {
      return $"cannot enable SeProfileSingleProcessPrivilege (error {Marshal.GetLastPInvokeError()})";
    }
    var command = WindowsNative.MemoryFlushModifiedList;
    var status = WindowsNative.NtSetSystemInformation(WindowsNative.SystemMemoryListInformation, &command, sizeof(int));
    if (status != 0) {
      return $"flush of the modified list failed (NTSTATUS 0x{status:x8})";
    }
    command = WindowsNative.MemoryPurgeStandbyList;
    status = WindowsNative.NtSetSystemInformation(WindowsNative.SystemMemoryListInformation, &command, sizeof(int));
    return status == 0 ? null : $"purge of the standby list failed (NTSTATUS 0x{status:x8})";
  }

  [SupportedOSPlatform("windows")]
  private static bool EnablePrivilege(string name) {
    if (!WindowsNative.OpenProcessToken(WindowsNative.GetCurrentProcess(), WindowsNative.TOKEN_ADJUST_PRIVILEGES | WindowsNative.TOKEN_QUERY, out var token)) {
      return false;
    }
    try {
      if (!WindowsNative.LookupPrivilegeValue(null, name, out var luid)) {
        return false;
      }
      var privileges = new WindowsNative.TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = WindowsNative.SE_PRIVILEGE_ENABLED };
      // Note: Succeeds without enabling the privilege if the token doesn't have it (ERROR_NOT_ALL_ASSIGNED).
      return WindowsNative.AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero) && Marshal.GetLastPInvokeError() == 0;
    } finally {
      WindowsNative.CloseHandle(token);
    }
  }
}
