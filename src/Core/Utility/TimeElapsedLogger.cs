// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Utility {
  /// <summary>
  /// Utility class to log memory usage and time spend in a block of code
  /// typically wrapped with a "using" statement of an instance of this class.
  /// </summary>
  public struct TimeElapsedLogger : IDisposable {
    [ThreadStatic]
    private static int _currentThreadIndent;

    private readonly string _description;
    private readonly Stopwatch _stopwatch;
    private readonly string _indent;

    public TimeElapsedLogger(string description) {
      _currentThreadIndent++;
      _description = description;
      _stopwatch = Stopwatch.StartNew();
      _indent = GetIndent(_currentThreadIndent);
      Logger.LogInfo("{0}{1}.", _indent, _description);
    }

    public void Dispose() {
      _currentThreadIndent--;
      _stopwatch.Stop();
      Logger.LogInfo("{0}{1} performed in {2:n0} msec.", _indent, _description, _stopwatch.ElapsedMilliseconds);
    }

    public string Indent {
      get { return _indent; }
    }

    public static string GetIndent(int indent) {
      switch (indent) {
        case 0:
          return "";
        case 1:
          return "++";
        case 2:
          return "++++";
        case 3:
          return "++++++";
        case 4:
          return "++++++++";
        case 5:
          return "++++++++++";
        case 6:
          return "++++++++++++";
        default:
          return new string('+', indent*2);
      }
    }
  }
}