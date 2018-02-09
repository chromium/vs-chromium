// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Features.ToolWindows.CodeSearch;
using VsChromium.Wpf;

namespace VsChromium.Features.IndexServerInfo {
  public class ProjectDetailsViewModel : ViewModelBase {
    private ProjectDetails _selectedProject = new ProjectDetails();
    private CommandDelegate _showProjectConfigurationCommand;

    public event EventHandler ShowProjectConfigurationInvoked;

    public ProjectDetails Details {
      get { return _selectedProject; }
      set { UpdateProperty(ref _selectedProject, value); }
    }

    public CommandDelegate ShowProjectConfigurationCommand {
      get {
        return _showProjectConfigurationCommand ?? (_showProjectConfigurationCommand =
                 new CommandDelegate(o => OnShowProjectConfigurationInvoked()));
      }
    }

    protected virtual void OnShowProjectConfigurationInvoked() {
      ShowProjectConfigurationInvoked?.Invoke(this, EventArgs.Empty);
    }
  }
}