// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Design;

namespace VsChromiumPackage.Features.ChromiumExplorer {
  public interface IChromiumExplorerToolWindowAccessor {
    ChromiumExplorerToolWindow GetToolWindow();
    void FocusSearchTextBox(CommandID commandId);
  }
}
