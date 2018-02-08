// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Features.ToolWindows.CodeSearch.IndexServerInfo {
  public class IndexServerInfoViewModel : ViewModelBase {
    private string _serverStatus;
    private string _indexStatus;
    private string _memoryStatus;
    private int _projectCount;
    private CommandDelegate _indexDetailsCommand;

    public event EventHandler IndexDetailsInvoked;
     
    public string ServerStatus {
      get { return _serverStatus; }
      set { UpdateProperty(ref _serverStatus, value); }
    }

    public string IndexStatus {
      get { return _indexStatus; }
      set { UpdateProperty(ref _indexStatus, value); }
    }

    public int ProjectCount {
      get { return _projectCount; }
      set {
        UpdateProperty(ref _projectCount, value);
        IndexDetailsCommand.Refresh();
      }
    }

    public string MemoryStatus {
      get { return _memoryStatus; }
      set { UpdateProperty(ref _memoryStatus, value); }
    }

    public CommandDelegate IndexDetailsCommand {
      get {
        return _indexDetailsCommand ?? (_indexDetailsCommand =
                 new CommandDelegate(o => OnIndexDetailsInvoked(), o => IndexDetailsCanExecute()));
      }
    }

    private bool IndexDetailsCanExecute() {
      return ProjectCount > 0;
    }

    protected virtual void OnIndexDetailsInvoked() {
      IndexDetailsInvoked?.Invoke(this, EventArgs.Empty);
    }
  }
}
