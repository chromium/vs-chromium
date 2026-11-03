// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.ProgressTracking;

namespace VsChromium.Server.Ipc.TypedEvents {
  /// <summary>
  /// Progress of long operations sent as <see cref="ProgressReportEvent"/> (typed messages protocol).
  /// </summary>
  public class TypedEventProgressSender : IProgressEventSender {
    private readonly ITypedEventSender _typedEventSender;

    public TypedEventProgressSender(ITypedEventSender typedEventSender) {
      _typedEventSender = typedEventSender;
    }

    public void SendProgress(string displayText, int completed, int total) {
      _typedEventSender.SendEventAsync(new ProgressReportEvent {
        DisplayText = displayText,
        Completed = completed,
        Total = total,
      });
    }
  }
}
