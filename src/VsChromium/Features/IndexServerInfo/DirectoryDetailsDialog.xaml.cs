// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Features.IndexServerInfo {
  public partial class DirectoryDetailsDialog {
    public DirectoryDetailsDialog() {
      InitializeComponent();
      DataContext = new DirectoryDetailsViewModel();
    }

    public DirectoryDetailsViewModel ViewModel => (DirectoryDetailsViewModel) DataContext;
  }
}
