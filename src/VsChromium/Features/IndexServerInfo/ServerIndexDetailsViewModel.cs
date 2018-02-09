// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Wpf;

namespace VsChromium.Features.IndexServerInfo {
  public class ServerIndexDetailsViewModel : ViewModelBase {
    private ProjectDetails _selectedProject;
    private bool _waiting = true;

    public List<ProjectDetails> Projects { get; } = new List<ProjectDetails>();

    public bool Waiting {
      get { return _waiting; }
      set { UpdateProperty(ref _waiting, value); }
    }

    public ProjectDetails SelectedProject {
      get { return _selectedProject; }
      set { UpdateProperty(ref _selectedProject, value); }
    }
  }
}