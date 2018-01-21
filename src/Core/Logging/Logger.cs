// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using NLog;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Logging {
  public class Logger {
    private static readonly Lazy<NLog.Logger> NLogLogger =
      new Lazy<NLog.Logger>(CreateNLogLogger, LazyThreadSafetyMode.ExecutionAndPublication);

    private static NLog.Logger CreateNLogLogger() {
      NLogConfig.ConfigureApplication(FileName, Id);
      return LogManager.GetCurrentClassLogger();
    }

    static Logger() {
      FileName = "VsChromium";
      Id = "VsChromium";
      Info = true;
      Perf = true;
      Warning = true;
      Error = true;
    }

    public static string FileName { get; set; }
    public static string Id { get; set; }

    public static bool Info { get; set; }
    public static bool Perf { get; set; }
    public static bool Warning { get; set; }
    public static bool Error { get; set; }

    public static void LogInfo(string format, params object[] args) {
      if (!Info)
        return;

      NLogLogger.Value.Info(format, args);
    }

    public static void LogPerf(string format, params object[] args) {
      if (!Perf)
        return;

      NLogLogger.Value.Info(format, args);
    }

    public static void LogWarning(string format, params object[] args) {
      if (!Warning)
        return;

      NLogLogger.Value.Warn(format, args);
    }

    public static void LogWarning(Exception exception, string format, params object[] args) {
      if (!Warning)
        return;

      NLogLogger.Value.Warn(exception, format, args);
    }

    public static void LogError(string format, params object[] args) {
      if (!Error)
        return;

      NLogLogger.Value.Error(format, args);
    }

    public static void LogError(Exception exception, string format, params object[] args) {
      if (!Error)
        return;

      NLogLogger.Value.Error(exception, format, args);
    }

    public static void LogHResult(int hresult, string format, params object[] args) {
      if (!Error)
        return;

      var exception = Marshal.GetExceptionForHR(hresult);
      if (exception == null) {
        LogError("{0} (hresult=0x{1:x8})",
          string.Format(format, args),
          hresult);
      } else {
        LogError("{0} (hresult=0x{1:x8}, message={2})",
          string.Format(format, args),
          hresult,
          exception.Message);
      }
    }

    public static void LogMemoryStats(string indent = "") {
      if (!Perf)
        return;

      var msg = "";
      msg += string.Format("{0}GC Memory: {1:n0} bytes.", indent, GC.GetTotalMemory(false));
      for (var i = 0; i <= GC.MaxGeneration; i++) {
        msg += string.Format(" Gen{0}: {1:n0}.", i, GC.CollectionCount(i));
      }
      msg += string.Format(" HeapAlloc Memory: {0:n0} bytes.", HeapAllocStatic.TotalMemory);
      NLogLogger.Value.Info(msg);
    }

    public static void WrapActionInvocation(Action action) {
      try {
        action();
      }
      catch (Exception e) {
        LogError(e, "Error during callback execution");
      }
    }
  }
}