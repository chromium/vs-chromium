// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  public class GetFileSystemVersionRequestHandler : TypedMessageRequestHandler<GetFileSystemVersionRequest> {
    private readonly IFileSystemSnapshotManager _snapshotManager;

    public GetFileSystemVersionRequestHandler(IFileSystemSnapshotManager snapshotManager) {
      _snapshotManager = snapshotManager;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var tree = _snapshotManager.CurrentSnapshot;
      return new GetFileSystemVersionResponse {
        Version = tree.Version
      };
    }
  }
}
