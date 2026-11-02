// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class ProjectDetails {
    public ProjectDetails() {
      DirectoryDetails = new DirectoryDetails();
      ConfigurationDetails = new ProjectConfigurationDetails();
    }

    public string RootPath { get; set; }

    public DirectoryDetails DirectoryDetails { get; set; }

    public ProjectConfigurationDetails ConfigurationDetails { get; set; }

    /// <summary>True if the trigram filters of the project are enabled (see SetProjectOptionsRequest).</summary>
    public bool TrigramFiltersEnabled { get; set; }

    /// <summary>Memory used by the trigram filters of the project, in bytes.</summary>
    public long TrigramFiltersBytes { get; set; }
  }
}
