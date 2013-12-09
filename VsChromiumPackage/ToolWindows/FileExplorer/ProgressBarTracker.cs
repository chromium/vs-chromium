// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using VsChromiumPackage.Wpf;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  /// <summary>
  /// This class is thread safe, but all operations will be run in the Dispatcher thread of the ProgressBar instance.
  /// </summary>
  public class ProgressBarTracker {
    private readonly List<ProgressBarItem> _items = new List<ProgressBarItem>();
    private readonly object _lock = new object();
    private readonly ProgressBar _progressBar;

    public ProgressBarTracker(ProgressBar progressBar) {
      this._progressBar = progressBar;
    }

    /// <summary>
    /// Enqueue a progress bar request for the given operation id.
    /// The progress bar will be shown only after a short delay (.5 sec).
    /// </summary>
    public void Start(string operationId, string toolTipText) {
      var item = AddOrUpdateOperation(operationId, toolTipText);

      // Start a new one
      WpfUtilities.Post(new DispatchAction {
        Dispatcher = this._progressBar.Dispatcher,
        Delay = TimeSpan.FromSeconds(0.3),
        Action = () => StartWorker(item)
      });
    }

    private ProgressBarItem AddOrUpdateOperation(string operationId, string toolTipText) {
      lock (this._lock) {
        CancelExistingOperation(operationId);
        return AddOperation(operationId, toolTipText);
      }
    }

    public void Stop(string operationId) {
      WpfUtilities.Post(new DispatchAction {
        Dispatcher = this._progressBar.Dispatcher,
        Delay = TimeSpan.FromSeconds(0.1),
        Action = () => StopWorker(operationId)
      });
    }

    private ProgressBarItem AddOperation(string operationId, string toolTipText) {
      var item = new ProgressBarItem {
        CancellationTokenSource = new CancellationTokenSource(),
        OperationId = operationId,
        ToolTipText = toolTipText
      };

      lock (this._lock) {
        this._items.Add(item);
      }
      return item;
    }

    /// <summary>
    /// Return "true" if the array of operations is empty after removal.
    /// </summary>
    private bool CancelExistingOperation(string operationId) {
      lock (this._lock) {
        var item = this._items.FirstOrDefault(x => x.OperationId == operationId);
        if (item != null) {
          item.CancellationTokenSource.Cancel();
          this._items.Remove(item);
        }
        return this._items.Count == 0;
      }
    }

    private void StartWorker(ProgressBarItem item) {
      if (item.CancellationTokenSource.IsCancellationRequested)
        return;

      // Update UI
      if (this._progressBar.Visibility != Visibility.Visible) {
        //Logger.Log("Showing progress bar for item: {0}.", item.ToolTipText);
        this._progressBar.IsIndeterminate = true;
        this._progressBar.Visibility = Visibility.Visible;
      }
      this._progressBar.ToolTip = item.ToolTipText;
    }

    private void StopWorker(string operationId) {
      var isEmpty = CancelExistingOperation(operationId);

      // Update UI
      if (isEmpty) {
        if (this._progressBar.Visibility == Visibility.Visible) {
          //Logger.Log("Hiding progress bar.");
          this._progressBar.Visibility = Visibility.Hidden;
          this._progressBar.IsIndeterminate = false;
        }
        this._progressBar.ToolTip = null;
      } else {
        //Logger.Log("Updating tooltip.");
        this._progressBar.ToolTip = this._items.Last().ToolTipText;
      }
    }

    private class ProgressBarItem {
      public string OperationId { get; set; }
      public CancellationTokenSource CancellationTokenSource { get; set; }
      public string ToolTipText { get; set; }
    }
  }
}
