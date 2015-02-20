// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Logging {
  public class LoggerConfiguration {
    public static readonly LoggerConfiguration Instance = new LoggerConfiguration();

    public LoggerConfiguration() {
#if DEBUG
      this.Info = true;
      this.Perf = true;
#endif
      this.Warning = true;
      this.Error = true;
    }

    public bool Info { get; set; }
    public bool Perf { get; set; }
    public bool Warning { get; set; }
    public bool Error { get; set; }
  }
}