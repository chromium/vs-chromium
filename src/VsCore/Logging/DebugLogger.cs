// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Logging {
  public class DebugLogger : ILogLevelLogger {
    public static readonly DebugLogger Instance = new DebugLogger();

    public bool Enabled => Logger.IsDebugEnabled;

    public void Log(string message) {
      Logger.LogDebug(message);
    }

    public void Log(string format, params object[] args) {
      Logger.LogDebug(format, args);
    }
  }
}