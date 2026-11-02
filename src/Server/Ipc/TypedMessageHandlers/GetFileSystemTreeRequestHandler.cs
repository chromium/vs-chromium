// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  public class GetFileSystemTreeRequestHandler : TypedMessageRequestHandler<GetFileSystemTreeRequest> {
    private readonly IFileSystemSnapshotManager _snapshotManager;

    public GetFileSystemTreeRequestHandler(IFileSystemSnapshotManager snapshotManager) {
      _snapshotManager = snapshotManager;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      return new GetFileSystemTreeResponse {
        Tree = _snapshotManager.CurrentSnapshot.ToIpcCompactFileSystemTree()
      };
    }
  }
}
