// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class ProjectConfigurationDetails {
    [ProtoMember(1)]
    public ProjectConfigurationSectionDetails IgnorePathsSection { get; set; }

    [ProtoMember(2)]
    public ProjectConfigurationSectionDetails IgnoreSearchableFilesSection { get; set; }

    [ProtoMember(3)]
    public ProjectConfigurationSectionDetails IncludeSearchableFilesSection { get; set; }
  }
}