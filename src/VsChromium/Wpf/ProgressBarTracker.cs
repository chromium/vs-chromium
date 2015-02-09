// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace VsChromium.Wpf {
  /// <summary>
  /// This class is thread safe, but all operations will be run in the
  /// Dispatcher thread of the ProgressBar instance.
  /// </summary>
  public class ProgressBarTracker : IProgressBarTracker {
    private readonly List<ProgressBarItem> _items = new List<ProgressBarItem>();
    private readonly object _lock = new object();
    private readonly ProgressBar _progressBar;

    public ProgressBarTracker(ProgressBar progressBar) {
      _progressBar = progressBar;
    }

    /// <summary>
    /// Enqueue a progress bar request for the given operation id. The progress
    /// bar will be shown only after a short delay (.3 sec).
    /// </summary>
    public void Start(string operationId, string toolTipText) {
      var item = AddOrUpdateOperation(operationId, toolTipText);

      // Start a new one
      WpfUtilities.Post(new DispatchAction {
        Dispatcher = _progressBar.Dispatcher,
        Delay = TimeSpan.FromSeconds(0.3),
        Action = () => StartWorker(item)
      });
    }

    /// <summary>
    /// Enqueue a progress bar "stop" request for the given operation id. The
    /// progress bar will be hidden only after a short delay (.1 sec) in case a
    /// new operation comes in.
    /// </summary>
    public void Stop(string operationId) {
      WpfUtilities.Post(new DispatchAction {
        Dispatcher = _progressBar.Dispatcher,
        Delay = TimeSpan.FromSeconds(0.1),
        Action = () => StopWorker(operationId)
      });
    }

    private ProgressBarItem AddOrUpdateOperation(string operationId, string toolTipText) {
      lock (_lock) {
        CancelExistingOperation(operationId);
        return AddOperation(operationId, toolTipText);
      }
    }

    private ProgressBarItem AddOperation(string operationId, string toolTipText) {
      var item = new ProgressBarItem {
        CancellationTokenSource = new CancellationTokenSource(),
        OperationId = operationId,
        ToolTipText = toolTipText
      };

      lock (_lock) {
        _items.Add(item);
      }
      return item;
    }

    /// <summary>
    /// Return "true" if the array of operations is empty after removal.
    /// </summary>
    private bool CancelExistingOperation(string operationId) {
      lock (_lock) {
        var item = _items.FirstOrDefault(x => x.OperationId == operationId);
        if (item != null) {
          item.CancellationTokenSource.Cancel();
          _items.Remove(item);
        }
        return _items.Count == 0;
      }
    }

    private void StartWorker(ProgressBarItem item) {
      if (item.CancellationTokenSource.IsCancellationRequested)
        return;

      // Update UI
      if (_progressBar.Visibility != Visibility.Visible) {
        //Logger.LogInfo("Showing progress bar for item: {0}.", item.ToolTipText);
        _progressBar.IsIndeterminate = true;
        _progressBar.Visibility = Visibility.Visible;
      }
      _progressBar.ToolTip = item.ToolTipText;
    }

    private void StopWorker(string operationId) {
      var isEmpty = CancelExistingOperation(operationId);

      // Update UI
      if (isEmpty) {
        if (_progressBar.Visibility == Visibility.Visible) {
          //Logger.LogInfo("Hiding progress bar.");
          _progressBar.Visibility = Visibility.Hidden;
          _progressBar.IsIndeterminate = false;
        }
        _progressBar.ToolTip = null;
      } else {
        //Logger.LogInfo("Updating tooltip.");
        _progressBar.ToolTip = _items.Last().ToolTipText;
      }
    }

    private class ProgressBarItem {
      public string OperationId { get; set; }
      public CancellationTokenSource CancellationTokenSource { get; set; }
      public string ToolTipText { get; set; }
    }
  }
}
