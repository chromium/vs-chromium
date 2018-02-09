// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows.Input;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Wpf;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class DirectoryDetailsViewModel : ViewModelBase {
    private DirectoryDetails _directoryDetails;
    private bool _waiting = true;
    private Cursor _cursor = Cursors.Wait;

    public Cursor Cursor {
      get { return _cursor; }
      set { UpdateProperty(ref _cursor, value); }
    }

    public bool Waiting {
      get { return _waiting; }
      set { UpdateProperty(ref _waiting, value); }
    }

    public DirectoryDetails DirectoryDetails {
      get { return _directoryDetails; }
      set {
        UpdateProperty(ref _directoryDetails, value);
        Waiting = (value == null);
        Cursor = (value == null ? Cursors.Wait : null);
      }
    }
  }
}