// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using VsChromiumCore.Win32.Memory;

namespace VsChromiumCore {
  public class Logger {
    public static void Log(string format, params object[] args) {
      Trace.WriteLine(string.Format(format, args));
    }

    public static void LogError(string format, params object[] args) {
      Log("ERROR: {0}", string.Format(format, args));
    }

    public static void LogException(Exception exception, string format, params object[] args) {
      var msg = string.Format(format, args);
      Log("ERROR: {0}", msg);
      for (var ex = exception; ex != null; ex = ex.InnerException) {
        Log("  Message:     {0}", ex.Message);
        Log("  Type:        {0}", ex.GetType().FullName);
        Log("  StackTrace:");
        Log("{0}", ex.StackTrace);
      }
    }

    public static void LogMemoryStats() {
      string msg = "";
      msg += string.Format("GC Memory: {0:n0} bytes.", GC.GetTotalMemory(false));
      for (var i = 0; i <= GC.MaxGeneration; i++) {
        msg += string.Format(" Gen{0}: {1:n0}.", i, GC.CollectionCount(i));
      }
      msg += string.Format(" HeapAlloc Memory: {0:n0} bytes.", HeapAllocStatic.TotalMemory);
      Logger.Log(msg);
    }
  }
}
