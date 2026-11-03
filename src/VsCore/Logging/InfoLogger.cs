// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Logging {
  public class InfoLogger : ILogLevelLogger {
    public static readonly InfoLogger Instance = new InfoLogger();

    public bool Enabled => Logger.IsInfoEnabled;

    public void Log(string message) {
      Logger.LogInfo(message);
    }

    public void Log(string format, params object[] args) {
      Logger.LogInfo(format, args);
    }
  }
}