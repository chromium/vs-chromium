// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Logging {
  public interface ILogLevelLogger {
    bool Enabled { get; }
    void Log(string message);
    void Log(string format, params object[] args);
  }
}