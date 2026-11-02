// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.ServerProxy {
  public interface IFileSystemTreeSource {
    void Fetch();

    event Action<FileSystemTree> TreeReceived;
    event Action<ErrorResponse> ErrorReceived;
  }
}