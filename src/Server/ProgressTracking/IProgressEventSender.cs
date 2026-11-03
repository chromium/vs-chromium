// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.ProgressTracking {
  /// <summary>
  /// Sends the progress of long operations to the client (from any thread): a
  /// <c>ProgressReportEvent</c> with the typed messages protocol, an "index/progress" notification
  /// with the JSON-RPC protocol.
  /// </summary>
  public interface IProgressEventSender {
    void SendProgress(string displayText, int completed, int total);
  }
}
