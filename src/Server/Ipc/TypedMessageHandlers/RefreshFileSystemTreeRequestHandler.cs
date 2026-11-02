// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  public class RefreshFileSystemTreeRequestHandler : TypedMessageRequestHandler {
    private readonly IIndexingServer _indexingServer;

    public RefreshFileSystemTreeRequestHandler(IIndexingServer indexingServer) {
      _indexingServer = indexingServer;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      _indexingServer.Refresh();
      return new RefreshFileSystemTreeResponse();
    }
  }
}
