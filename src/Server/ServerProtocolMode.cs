// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server {
  /// <summary>
  /// The protocol spoken by a server process (exactly one, chosen by its command line).
  /// </summary>
  public enum ServerProtocolMode {
    /// <summary>
    /// "--stdio": the typed messages protocol, one JSON message per line (removed when both
    /// clients use the JSON-RPC protocol).
    /// </summary>
    TypedMessages,

    /// <summary>
    /// "--jsonrpc": JSON-RPC 2.0 with the framing of the Language Server Protocol (see
    /// protocol/README.md).
    /// </summary>
    JsonRpc,
  }
}
