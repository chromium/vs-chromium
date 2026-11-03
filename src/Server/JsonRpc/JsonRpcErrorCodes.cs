// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// The error codes of the JSON-RPC responses of the server (see "Transport" in
  /// protocol/README.md).
  /// </summary>
  public static class JsonRpcErrorCodes {
    /// <summary>Invalid JSON (JSON-RPC 2.0). The response has a null id.</summary>
    public const int ParseError = -32700;

    /// <summary>The message is not a valid JSON-RPC 2.0 request (JSON-RPC 2.0).</summary>
    public const int InvalidRequest = -32600;

    /// <summary>Unknown method (JSON-RPC 2.0).</summary>
    public const int MethodNotFound = -32601;

    /// <summary>Invalid parameters of the method (JSON-RPC 2.0).</summary>
    public const int InvalidParams = -32602;

    /// <summary>Unexpected error of the server; "data" is an <c>ErrorInfo</c> (JSON-RPC 2.0).</summary>
    public const int InternalError = -32603;

    /// <summary>
    /// Expected error, shown to the user as is (e.g. invalid regular expression, unknown project).
    /// In the range reserved for implementation defined server errors.
    /// </summary>
    public const int RecoverableError = -32001;

    /// <summary>
    /// The request was cancelled ("$/cancelRequest", or a search cancelled by a newer search):
    /// "RequestCancelled" of the Language Server Protocol.
    /// </summary>
    public const int RequestCancelled = -32800;
  }
}
