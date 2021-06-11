// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Wpf;

namespace VsChromium.Features.IndexServerInfo {
  public class ServerDetailsViewModel : ViewModelBase {
    private ProjectDetailsViewModel _selectedProject;
    private bool _waiting = true;

    public List<ProjectDetailsViewModel> Projects { get; } = new List<ProjectDetailsViewModel>();

    public bool Waiting {
      get { return _waiting; }
      set { UpdateProperty(ref _waiting, value); }
    }

    public ProjectDetailsViewModel SelectedProject {
      get { return _selectedProject; }
      set { UpdateProperty(ref _selectedProject, value); }
    }
  }
}