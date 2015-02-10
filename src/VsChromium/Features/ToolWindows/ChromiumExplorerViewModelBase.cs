// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using VsChromium.Core.Linq;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows {
  public class ChromiumExplorerViewModelBase : INotifyPropertyChanged {
    private readonly TreeViewRootNodes<TreeViewItemViewModel> _rootNodes = new TreeViewRootNodes<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _activeRootNodes;

    /// <summary>
    /// Databound!
    /// </summary>
    public TreeViewRootNodes<TreeViewItemViewModel> RootNodes { get { return _rootNodes; } }

    public List<TreeViewItemViewModel> ActiveRootNodes { get { return _activeRootNodes; } }

    public IStandarImageSourceFactory ImageSourceFactory { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    public event EventHandler RootNodesChanged;

    protected void SetRootNodes(List<TreeViewItemViewModel> newRootNodes, params string[] defaultText) {
      // Don't update if we are passed in the already active collection.
      if (object.ReferenceEquals(_activeRootNodes, newRootNodes))
        return;
      _activeRootNodes = newRootNodes;

      // Move the active root nodes into the observable collection so that
      // the TreeView is refreshed.
      _rootNodes.Clear();
      if (_activeRootNodes.Count == 0) {
        if (defaultText != null && defaultText.Length > 0) {
          var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
          foreach (var text in defaultText) {
            _rootNodes.Add(new TextItemViewModel(ImageSourceFactory, rootNode, text));
          }
          _rootNodes.ForAll(rootNode.AddChild);
          TreeViewItemViewModel.ExpandNodes(_rootNodes, true);
        }
      } else {
        _activeRootNodes.ForAll(x => _rootNodes.Add(x));
      }
      this.OnRootNodesChanged();
    }


    protected virtual void OnRootNodesChanged() {
      var handler = RootNodesChanged;
      if (handler != null) handler(this, EventArgs.Empty);
    }

    protected virtual void OnPropertyChanged(string propertyName) {
      PropertyChangedEventHandler handler = PropertyChanged;
      if (handler != null)
        handler(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
