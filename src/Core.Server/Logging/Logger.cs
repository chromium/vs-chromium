// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using NLog;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;

namespace VsChromium.Core.Logging {
  public static class Logger {
    private static readonly Lazy<NLog.Logger> NLogLogger =
      new Lazy<NLog.Logger>(CreateNLogLogger, LazyThreadSafetyMode.ExecutionAndPublication);

    private static NLog.Logger CreateNLogLogger() {
      NLogConfig.ConfigureApplication(FileName, Id);
      return LogManager.GetCurrentClassLogger();
    }

    static Logger() {
      // The indexing server (modern .NET, see Core.Server.csproj) logs to its own files, separate from
      // the Visual Studio side logs (VsChromium.log).
      FileName = "VsChromium.Server";
      Id = "VsChromium";
#if DEBUG
      IsDebugEnabled = true;
#endif
      IsInfoEnabled = true;
      IsWarnEnabled = true;
      IsErrorEnabled = true;
    }

    public static string FileName { get; set; }
    public static string Id { get; set; }

    public static bool IsDebugEnabled { get; set; }
    public static bool IsInfoEnabled { get; set; }
    public static bool IsWarnEnabled { get; set; }
    public static bool IsErrorEnabled { get; set; }

    public static void LogDebug(string format, params object[] args) {
      if (!IsDebugEnabled)
        return;

      NLogLogger.Value.Debug(format, args);
    }

    public static void LogInfo(string format, params object[] args) {
      if (!IsInfoEnabled)
        return;

      NLogLogger.Value.Info(format, args);
    }

    public static void LogInfo(Exception exception, string format, params object[] args) {
      if (!IsInfoEnabled)
        return;

      NLogLogger.Value.Info(format, args);
      LogException(LogLevel.Info, exception);
    }

    public static void LogWarn(string format, params object[] args) {
      if (!IsWarnEnabled)
        return;

      NLogLogger.Value.Warn(format, args);
    }

    public static void LogWarn(Exception exception, string format, params object[] args) {
      if (!IsWarnEnabled)
        return;

      NLogLogger.Value.Warn(format, args);
      LogException(LogLevel.Warn, exception);
    }

    public static void LogError(Exception exception, string format, params object[] args) {
      if (!IsErrorEnabled)
        return;

      NLogLogger.Value.Error(format, args);
      LogException(LogLevel.Error, exception);
    }

    public static void LogMemoryStats(string indent = "") {
      if (!IsDebugEnabled)
        return;

      var msg = "";
      msg += string.Format("{0}GC Memory: {1:n0} bytes.", indent, GC.GetTotalMemory(false));
      for (var i = 0; i <= GC.MaxGeneration; i++) {
        msg += string.Format(" Gen{0}: {1:n0}.", i, GC.CollectionCount(i));
      }
      msg += string.Format(" Native Memory: {0:n0} bytes.", NativeHeap.TotalMemory);
      NLogLogger.Value.Info(msg);
    }

    public static void LogException(LogLevel logLevel, Exception e) {
      LogExceptionWorker(logLevel, 0, e);
    }

    public static void LogExceptionWorker(LogLevel logLevel, int indent, Exception e) {
      if (e == null) {
        return;
      }

      var strIndent = new string(' ', indent * 2);
      NLogLogger.Value.Log(logLevel, "{0} Exception Type: {1}", strIndent, e.GetType());
      NLogLogger.Value.Log(logLevel, "{0} Exception Message: {1}", strIndent, e.Message.Replace("\r\n", "\\r\\n"));
      var stackTrace = e.StackTrace;
      if (string.IsNullOrWhiteSpace(stackTrace)) {
        NLogLogger.Value.Log(logLevel, "{0} + {1}", strIndent, "<No Stack Trace>");
      } else {
        using (var reader = new StringReader(e.StackTrace)) {
          for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
            if (line.Length > 0) {
              NLogLogger.Value.Log(logLevel, "{0} + {1}", strIndent, line);
            }
          }
        }
      }

      LogExceptionWorker(logLevel, indent + 1, e.InnerException);
    }

    public static void WrapActionInvocation(Action action) {
      try {
        action();
      } catch (Exception e) {
        LogError(e, "Error during callback execution");
      }
    }
  }
}
