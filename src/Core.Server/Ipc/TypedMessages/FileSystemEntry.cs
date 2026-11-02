// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public abstract class FileSystemEntry {
    protected FileSystemEntry() {
      // To avoid getting "null" property.
      Name = string.Empty;
    }

    public string Name { get; set; }

    public FileSystemEntryData Data { get; set; }

    public abstract override string ToString();
  }
}
