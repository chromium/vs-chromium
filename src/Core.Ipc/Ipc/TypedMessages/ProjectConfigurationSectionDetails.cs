// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class ProjectConfigurationSectionDetails {
    public string ContainingFilePath  { get; set; }

    public string Name { get; set; }

    public string Contents { get; set; }
  }
}
