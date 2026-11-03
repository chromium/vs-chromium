// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Protocol.Generated;

namespace VsChromium.Features.IndexServerInfo {
  public partial class ProjectConfigurationSectionDetailsControl {

    public ProjectConfigurationSectionDetailsControl() {
      InitializeComponent();
      DataContext = new ProjectConfigurationSection();
    }

    public ProjectConfigurationSection ViewModel => (ProjectConfigurationSection)DataContext;
  }
}
