// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using NLog;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromium.Core.Files;
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
#if DEBUG
      IsDebugEnabled = true;
#endif
      IsInfoEnabled = true;
      IsWarnEnabled = true;
      IsErrorEnabled = true;
    }

    public static string FileName { get; set; }
    public static string Id { get; set; }

    public static string LogInfoPath {
      get { return GetLogFilePath(""); }
    }

    public static string LogErrorPath {
      get { return GetLogFilePath(".errors"); }
    }

    private static string GetLogFilePath(string suffix) {
      var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      var dirName = "VsChromium";
      return String.Format("{0}",
        PathHelpers.CombinePaths(PathHelpers.CombinePaths(appData, dirName), FileName + suffix + ".log"));
    }

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

    public static void LogError(string format, params object[] args) {
      if (!IsErrorEnabled)
        return;

      NLogLogger.Value.Error(format, args);
    }

    public static void LogError(Exception exception, string format, params object[] args) {
      if (!IsErrorEnabled)
        return;

      NLogLogger.Value.Error(format, args);
      LogException(LogLevel.Error, exception);
    }

    public static void LogHResult(int hresult, string format, params object[] args) {
      if (!IsErrorEnabled)
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
      if (!IsDebugEnabled)
        return;

      var msg = "";
      msg += string.Format("{0}GC Memory: {1:n0} bytes.", indent, GC.GetTotalMemory(false));
      for (var i = 0; i <= GC.MaxGeneration; i++) {
        msg += string.Format(" Gen{0}: {1:n0}.", i, GC.CollectionCount(i));
      }
      msg += string.Format(" HeapAlloc Memory: {0:n0} bytes.", HeapAllocStatic.TotalMemory);
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

    public static void TestLogException() {
      try {
        try {
          var s = "foo";
          s.Substring(10);
        }
        catch (Exception e) {
          throw new ApplicationException("Test Exception message", e);
        }
      } catch (Exception e) {
        Logger.LogInfo(e, "Test exception");
      }
    }
  }
}