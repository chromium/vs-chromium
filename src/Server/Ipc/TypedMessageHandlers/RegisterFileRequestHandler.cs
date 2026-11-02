// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  public class RegisterFileRequestHandler : TypedMessageRequestHandler<RegisterFileRequest> {
    private readonly IFileRegistrationTracker _fileRegistrationTracker;

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
