// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsChromium.Core.Chromium;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public class ChromeProcessViewModel {
    private ChromiumProcess _process;
    private BuildExplorerViewModel _root;
    private List<ChromeProcessViewModel> _childProcesses;
    private ImageSource _icon;

    public ChromeProcessViewModel(BuildExplorerViewModel root, ChromiumProcess process) {
      _root = root;
      _process = process;
      _childProcesses = new List<ChromeProcessViewModel>();
      _icon = Imaging.CreateBitmapSourceFromHIcon(
          _process.Icon.Handle,
          Int32Rect.Empty,
          BitmapSizeOptions.FromEmptyOptions());
    }

    public string DisplayText {
      get {
        return String.Format("{0} [PID: {1}]", _process.Category.ToGroupTitle(), _process.Pid);
      }
    }

    public void LoadProcesses(ChromiumProcess[] chromes) {
      foreach (ChromiumProcess chrome in chromes) {
        if (chrome.ParentPid == _process.Pid) {
          ChromeProcessViewModel viewModel = new ChromeProcessViewModel(_root, chrome);
          viewModel.LoadProcesses(chromes.ToArray());
          _childProcesses.Add(viewModel);
        }
      }
    }

    public bool IsDebugging {
      get { return false; }
    }

    public bool IsNotDebugging {
      get { return !IsDebugging; }
    }

    public IList<ChromeProcessViewModel> ChildProcesses {
      get { return _childProcesses; }
    }

    public ImageSource IconImage {
      get {
        return _icon;
      }
    }
  }
}
