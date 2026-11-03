// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json;
using VsChromium.Protocol.Generated;
using VsChromium.Server.ProgressTracking;

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// Sends the notifications of the protocol (server to client) over a <see cref="JsonRpcOutput"/>.
  /// Also sends the progress of long operations ("index/progress").
  /// </summary>
  public sealed class JsonRpcNotificationSender : ProtocolNotificationSender, IProgressEventSender {
    private readonly JsonRpcOutput _output;

    public JsonRpcNotificationSender(JsonRpcOutput output) {
      _output = output;
    }

    protected override void SendNotification(string method, JsonElement? parameters) {
      _output.SendNotification(method, parameters);
    }

    public void SendProgress(string displayText, int completed, int total) {
      IndexProgress(new ProgressParams {
        DisplayText = displayText ?? "",
        Completed = completed,
        Total = total,
      });
    }
  }
}
