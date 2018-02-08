// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.ToolWindows.CodeSearch.IndexServerInfo {
  public class IndexDetailsViewModel : ViewModelBase {
    private ProjectDetails _selectedProject;

    public List<ProjectDetails> Projects { get; } = new List<ProjectDetails>();

    public ProjectDetails SelectedProject {
      get { return _selectedProject; }
      set { UpdateProperty(ref _selectedProject, value); }
    }
  }
}