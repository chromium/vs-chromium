// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VsChromium.Features.ToolWindows.CodeSearch.IndexServerInfo {
  public class IndexServerInfoViewModel : INotifyPropertyChanged {
    private string _serverStatus;

    public event PropertyChangedEventHandler PropertyChanged;

    public string ServerStatus {
      get { return _serverStatus; }
      set { UpdateProperty(ref _serverStatus, value); }
    }

    protected void UpdateProperty<T>(ref T propertyField, T value, [CallerMemberName] string propertyName = null) {
      if (Equals(propertyField, value)) {
        return;
      }

      propertyField = value;
      OnPropertyChanged(propertyName);
    }
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
