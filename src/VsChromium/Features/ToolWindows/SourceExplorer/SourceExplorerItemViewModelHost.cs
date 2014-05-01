// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  [Export(typeof(ISourceExplorerItemViewModelHost))]
  public class SourceExplorerItemViewModelHost : ISourceExplorerItemViewModelHost {
    private readonly IUIRequestProcessor _uiRequestProcessor;
    private readonly IStandarImageSourceFactory _standarImageSourceFactory;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IOpenDocumentHelper _openDocumentHelper;

    [ImportingConstructor]
    public SourceExplorerItemViewModelHost(IUIRequestProcessor uiRequestProcessor,
                                           IStandarImageSourceFactory standarImageSourceFactory,
                                           ISynchronizationContextProvider synchronizationContextProvider,
                                           IOpenDocumentHelper openDocumentHelper) {
      _uiRequestProcessor = uiRequestProcessor;
      _standarImageSourceFactory = standarImageSourceFactory;
      _synchronizationContextProvider = synchronizationContextProvider;
      _openDocumentHelper = openDocumentHelper;
    }

    public IUIRequestProcessor UIRequestProcessor { get { return _uiRequestProcessor; } }

    public IStandarImageSourceFactory StandarImageSourceFactory { get { return _standarImageSourceFactory; } }

    public ISynchronizationContextProvider SynchronizationContextProvider { get { return _synchronizationContextProvider; } }

    public IOpenDocumentHelper OpenDocumentHelper { get { return _openDocumentHelper; } }
  }
}