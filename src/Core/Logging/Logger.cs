// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Threading;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Logging {
  public class Logger {
    private static readonly Lazy<string> ProcessName = new Lazy<string>(() => Process.GetCurrentProcess().ProcessName, LazyThreadSafetyMode.PublicationOnly);

    static Logger() {
#if DEBUG
      Info = true;
      Perf = true;
#endif
      Perf = true;
      Warning = true;
      Error = true;
    }

    public static bool Info { get; set; }
    public static bool Perf { get; set; }
    public static bool Warning { get; set; }
    public static  bool Error { get; set; }

    private static string GetLoggerId() {
      return ProcessName.Value;
    }

    private static void LogImpl(string format, params object[] args) {
      var message = string.Format(format, args);
      Trace.WriteLine(string.Format("[{0}:tid={1}] {2}", GetLoggerId(), Thread.CurrentThread.ManagedThreadId, message));
    }

    public static void LogInfo(string format, params object[] args) {
      if (!Logger.Info)
        return;

      LogImpl(format, args);
    }

    public static void LogPerf(string format, params object[] args) {
      if (!Logger.Perf)
        return;

      LogImpl(format, args);
    }

    public static void LogWarning(string format, params object[] args) {
      if (!Logger.Warning)
        return;

      LogImpl(format, args);
    }

    public static void LogWarning(Exception exception, string format, params object[] args) {
      if (!Logger.Warning)
        return;

      LogImpl(format, args);
      LogException(exception);
    }

    public static void LogError(string format, params object[] args) {
      if (!Logger.Error)
        return;

      LogImpl("ERROR: {0}", string.Format(format, args));
    }

    public static void LogError(Exception exception, string format, params object[] args) {
      if (!Logger.Error)
        return;

      var msg = string.Format(format, args);
      LogImpl("ERROR: {0}", msg);
      LogException(exception);
    }

    public static void LogMemoryStats(string indent = "") {
      if (!Logger.Perf)
        return;

      var msg = "";
      msg += string.Format("{0}GC Memory: {1:n0} bytes.", indent, GC.GetTotalMemory(false));
      for (var i = 0; i <= GC.MaxGeneration; i++) {
        msg += string.Format(" Gen{0}: {1:n0}.", i, GC.CollectionCount(i));
      }
      msg += string.Format(" HeapAlloc Memory: {0:n0} bytes.", HeapAllocStatic.TotalMemory);
      LogImpl(msg);
    }

    public static void WrapActionInvocation(Action action) {
      try {
        action();
      }
      catch (Exception e) {
        Logger.LogError(e, "Error during callback execution");
      }
    }
    private static void LogException(Exception exception) {
      for (var ex = exception; ex != null; ex = ex.InnerException) {
        LogImpl("  Message:     {0}", ex.Message);
        LogImpl("  Type:        {0}", ex.GetType().FullName);
        LogImpl("  StackTrace:");
        LogImpl("{0}", ex.StackTrace);
      }
    }
  }
}
