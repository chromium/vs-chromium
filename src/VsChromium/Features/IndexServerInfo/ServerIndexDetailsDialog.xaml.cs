// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Features.IndexServerInfo {
  /// <summary>
  /// Interaction logic for IndexDetailsDialog.xaml
  /// </summary>
  public partial class IndexDetailsDialog {
    public IndexDetailsDialog() {
      InitializeComponent();
      DataContext = new ServerIndexDetailsViewModel();
    }

    public ServerIndexDetailsViewModel ViewModel => (ServerIndexDetailsViewModel)DataContext;
  }
}
