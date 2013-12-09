// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystem;

namespace VsChromiumServer.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class AddFileNameRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemProcessor _processor;

    [ImportingConstructor]
    public AddFileNameRequestHandler(IFileSystemProcessor processor) {
      this._processor = processor;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      this._processor.AddFile(((AddFileNameRequest)typedRequest).FileName);

      return new DoneResponse {
        Info = "processing..."
      };
    }
  }
}
