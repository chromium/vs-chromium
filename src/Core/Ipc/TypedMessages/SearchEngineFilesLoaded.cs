// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class SearchEngineFilesLoaded : PairedTypedEvent {
    /// <summary>
    /// The version of the file system tree for which files have been loaded.
    /// </summary>
    public long TreeVersion { get; set; }
  }
}
