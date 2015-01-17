// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Threading;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Logging {
  public class Logger {
    private static readonly Lazy<string> _processName = new Lazy<string>(() => Process.GetCurrentProcess().ProcessName, LazyThreadSafetyMode.PublicationOnly);
    private static string GetLoggerId() {
      return _processName.Value;
    }

    private static void LogImpl(string format, params object[] args) {
      var message = string.Format(format, args);
      Trace.WriteLine(string.Format("[{0}:tid={1}] {2}", GetLoggerId(), Thread.CurrentThread.ManagedThreadId, message));
    }

    public static void Log(string format, params object[] args) {
      LogImpl(format, args);
    }

    public static void LogError(string format, params object[] args) {
      LogImpl("ERROR: {0}", string.Format(format, args));
    }

    public static void LogException(Exception exception, string format, params object[] args) {
      var msg = string.Format(format, args);
      LogImpl("ERROR: {0}", msg);
      for (var ex = exception; ex != null; ex = ex.InnerException) {
        LogImpl("  Message:     {0}", ex.Message);
        LogImpl("  Type:        {0}", ex.GetType().FullName);
        LogImpl("  StackTrace:");
        LogImpl("{0}", ex.StackTrace);
      }
    }

    public static void LogMemoryStats(string indent = "") {
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
        Logger.LogException(e, "Error calling action in DelayedOperationProcessor");
      }
    }
  }
}
