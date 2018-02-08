// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.ToolWindows.CodeSearch.IndexServerInfo {
  /// <summary>
  /// Interaction logic for IndexDetailsDialog.xaml
  /// </summary>
  public partial class IndexDetailsDialog {
    public IndexDetailsDialog() {
      InitializeComponent();
      DataContext = new IndexDetailsViewModel();
    }

    public IndexDetailsViewModel ViewModel => (IndexDetailsViewModel)DataContext;
  }
}
