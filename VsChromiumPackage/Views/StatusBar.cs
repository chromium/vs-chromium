// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromiumCore;

namespace VsChromiumPackage.Views {
  [Export(typeof(IStatusBar))]
  public class StatusBar : IStatusBar {
    private readonly IServiceProvider _serviceProvider;
    private object _animationIcon;
    private uint _cookie;
    private IVsStatusbar _statusBar;

    [ImportingConstructor]
    public StatusBar([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
      this._serviceProvider = serviceProvider;
    }

    public void ReportProgress(string displayText, int completed, int total) {
      if (completed >= total) {
        Logger.Log("Stopping progress at {0:n0} completed operations.", completed);
        StopProgress();
        return;
      }

      if (this._statusBar == null) {
        Logger.Log("Starting progress of {0:n0} total operations.", total);
        this._statusBar = this._serviceProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
        if (this._statusBar == null)
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
      if (this._animationIcon == null) {
        this._animationIcon = (short)Constants.SBAI_Find;
        this._statusBar.Animation(1, ref this._animationIcon);
      }

      this._statusBar.SetText(displayText);
    }

    private void ShowProgress(string displayText, int completed, int total) {
      this._statusBar.Progress(ref this._cookie, 1, displayText, (uint)completed, (uint)total);
    }

    private void StopProgress() {
      if (this._statusBar == null)
        return;

      // Clear the animation
      if (this._animationIcon != null) {
        this._statusBar.Animation(0, ref this._animationIcon);
        this._statusBar.SetText("");
        this._animationIcon = null;
      }

      // Clear the progress bar.
      if (this._cookie != 0) {
        this._statusBar.Progress(ref this._cookie, 0, "", 0, 0);
        this._cookie = 0;
      }

      this._statusBar = null;
    }
  }
}
