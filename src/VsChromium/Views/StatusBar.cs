// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.Logging;

namespace VsChromium.Views {
  [Export(typeof(IStatusBar))]
  public class StatusBar : IStatusBar {
    private readonly IServiceProvider _serviceProvider;
    private object _animationIcon;
    private uint _cookie;
    private IVsStatusbar _statusBar;

    [ImportingConstructor]
    public StatusBar([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
      _serviceProvider = serviceProvider;
    }

    public void ReportProgress(string displayText, int completed, int total) {
      if (completed >= total) {
        Logger.Log("Stopping progress at {0:n0} completed operations.", completed);
        StopProgress();
        return;
      }

      if (_statusBar == null) {
        Logger.Log("Starting progress of {0:n0} total operations.", total);
        _statusBar = _serviceProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
        if (_statusBar == null)
          return;
      }

      if (total == int.MaxValue) {
        ShowIndterminateProgress(displayText);
      } else {
        ShowProgress(displayText, completed, total);
      }
    }

    private void ShowIndterminateProgress(string displayText) {
      // Display the animated Visual Studio icon in the Animation region.
      if (_animationIcon == null) {
        _animationIcon = (short)Constants.SBAI_Find;
        _statusBar.Animation(1, ref _animationIcon);
      }

      _statusBar.SetText(displayText);
    }

    private void ShowProgress(string displayText, int completed, int total) {
      _statusBar.Progress(ref _cookie, 1, displayText, (uint)completed, (uint)total);
    }

    private void StopProgress() {
      if (_statusBar == null)
        return;

      // Clear the animation
      if (_animationIcon != null) {
        _statusBar.Animation(0, ref _animationIcon);
        _statusBar.SetText("");
        _animationIcon = null;
      }

      // Clear the progress bar.
      if (_cookie != 0) {
        _statusBar.Progress(ref _cookie, 0, "", 0, 0);
        _cookie = 0;
      }

      _statusBar = null;
    }
  }
}
