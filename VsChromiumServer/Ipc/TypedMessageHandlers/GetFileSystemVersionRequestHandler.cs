// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystem;

namespace VsChromiumServer.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetFileSystemVersionRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemProcessor _processor;

    [ImportingConstructor]
    public GetFileSystemVersionRequestHandler(IFileSystemProcessor processor) {
      this._processor = processor;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var tree = this._processor.GetTree();
      return new GetFileSystemVersionResponse {
        Version = tree.Version
      };
    }
  }
}
