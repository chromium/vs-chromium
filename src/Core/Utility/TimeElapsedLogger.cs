// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Threading;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Utility {
  /// <summary>
  /// Utility class to measure and log time spent in a block of code typically
  /// wrapped with a "using" statement of an instance of this class.
  /// </summary>
  public class TimeElapsedLogger : IDisposable {
    private static readonly ILogLevelLogger DefaultLogger = DebugLogger.Instance;
    private static readonly CancellationToken CancellationTokenInstance = new CancellationTokenSource().Token;

    [ThreadStatic]
    private static int _currentThreadIndent;

    private readonly string _description;
    private readonly CancellationToken _cancellationToken;
    private readonly ILogLevelLogger _logger;
    private readonly Stopwatch _stopwatch;

    public TimeElapsedLogger(string description)
      : this(description, CancellationTokenInstance, DefaultLogger) {
    }

    public TimeElapsedLogger(string description, CancellationToken cancellationToken)
      : this(description, cancellationToken, DefaultLogger) {
    }

    public TimeElapsedLogger(string description, ILogLevelLogger logger) 
      : this(description, CancellationTokenInstance, logger) {
    }

    public TimeElapsedLogger(string description, CancellationToken cancellationToken, ILogLevelLogger logger) {
      _currentThreadIndent++;
      _description = description;
      _cancellationToken = cancellationToken;
      _logger = logger;
      _stopwatch = Stopwatch.StartNew();
      if (logger.Enabled) {
        logger.Log("{0}{1}.", GetOpenIndent(_currentThreadIndent), _description);
      }
    }

    public void Dispose() {
      _stopwatch.Stop();
      if (_logger.Enabled) {
        _logger.Log(
          "{0}{1} {2} {3:n0} msec - GC Memory: {4:n0} bytes.",
          GetCloseIndent(_currentThreadIndent),
          _description,
          _cancellationToken.IsCancellationRequested ? "cancelled after" : "completed in",
          _stopwatch.ElapsedMilliseconds,
          GC.GetTotalMemory(false));
      }
      _currentThreadIndent--;
    }

    public static string GetOpenIndent(int indent) {
      switch (indent) {
        case 0:
          return "";
        case 1:
          return ">> ";
        case 2:
          return ">>>> ";
        case 3:
          return ">>>>>> ";
        case 4:
          return ">>>>>>>> ";
        case 5:
          return ">>>>>>>>>> ";
        case 6:
          return ">>>>>>>>>>>> ";
        default:
          return new string('>', indent * 2);
      }
    }

    public static string GetCloseIndent(int indent) {
      switch (indent) {
        case 0:
          return "";
        case 1:
          return "<< ";
        case 2:
          return "<<<< ";
        case 3:
          return "<<<<<< ";
        case 4:
          return "<<<<<<<< ";
        case 5:
          return "<<<<<<<<<< ";
        case 6:
          return "<<<<<<<<<<<< ";
        default:
          return new string('<', indent * 2);
      }
    }

  }
}