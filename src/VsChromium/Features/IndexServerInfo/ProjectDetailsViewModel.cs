// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Features.ToolWindows.CodeSearch;
using VsChromium.Wpf;

namespace VsChromium.Features.IndexServerInfo {
  public class ProjectDetailsViewModel : ViewModelBase {
    private ProjectDetails _projectDetails;
    private bool _waiting = true;
    private CommandDelegate _showProjectConfigurationCommand;

    public event EventHandler ShowProjectConfigurationInvoked;

    public bool Waiting {
      get { return _waiting; }
      set { UpdateProperty(ref _waiting, value); }
    }

    public ProjectDetails ProjectDetails {
      get { return _projectDetails; }
      set {
        UpdateProperty(ref _projectDetails, value);
        Waiting = (value == null);
      }
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