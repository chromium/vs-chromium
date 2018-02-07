// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VsChromium.Features.ToolWindows.CodeSearch.IndexServerInfo {
  public partial class IndexServerInfoDialog {
    public IndexServerInfoDialog() {
      InitializeComponent();
    }

    public IndexServerInfoViewModel ViewModel { get { return (IndexServerInfoViewModel)DataContext; } }
  }
}
