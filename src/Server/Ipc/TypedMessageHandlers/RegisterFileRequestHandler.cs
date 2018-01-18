// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class RegisterFileRequestHandler : TypedMessageRequestHandler {
    private readonly IFileRegistrationTracker _fileRegistrationTracker;

    [ImportingConstructor]
    public RegisterFileRequestHandler(IFileRegistrationTracker fileRegistrationTracker) {
      _fileRegistrationTracker = fileRegistrationTracker;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      _fileRegistrationTracker.RegisterFileAsync(new FullPath(((RegisterFileRequest)typedRequest).FileName));

      return new DoneResponse {
        Info = "processing..."
      };
    }
  }
}
