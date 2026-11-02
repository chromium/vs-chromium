// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class ProjectDetails {
    public ProjectDetails() {
      DirectoryDetails = new DirectoryDetails();
      ConfigurationDetails = new ProjectConfigurationDetails();
    }

    [ProtoMember(1)]
    public string RootPath { get; set; }

    [ProtoMember(2)]
    public DirectoryDetails DirectoryDetails { get; set; }

    [ProtoMember(3)]
    public ProjectConfigurationDetails ConfigurationDetails { get; set; }
  }
}