// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class TypedMessage : IpcMessageData {
    public TypedMessage() {
      ClassName = GetType().Name;
    }

    public string ClassName { get; set; }
  }
}
