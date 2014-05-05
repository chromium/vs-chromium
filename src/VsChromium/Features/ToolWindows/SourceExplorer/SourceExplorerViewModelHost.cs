// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class SourceExplorerViewModelHost : ISourceExplorerViewModelHost {
    private readonly SourceExplorerControl _control;
    private readonly IUIRequestProcessor _uiRequestProcessor;
    private readonly IStandarImageSourceFactory _standarImageSourceFactory;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IOpenDocumentHelper _openDocumentHelper;

    public SourceExplorerViewModelHost(
      SourceExplorerControl control,
      IUIRequestProcessor uiRequestProcessor,
      IStandarImageSourceFactory standarImageSourceFactory,
      ISynchronizationContextProvider synchronizationContextProvider,
      IOpenDocumentHelper openDocumentHelper) {
      _control = control;
      _uiRequestProcessor = uiRequestProcessor;
      _standarImageSourceFactory = standarImageSourceFactory;
      _synchronizationContextProvider = synchronizationContextProvider;
      _openDocumentHelper = openDocumentHelper;
    }

    public IUIRequestProcessor UIRequestProcessor { get { return _uiRequestProcessor; } }
    public IStandarImageSourceFactory StandarImageSourceFactory { get { return _standarImageSourceFactory; } }
    public ISynchronizationContextProvider SynchronizationContextProvider { get { return _synchronizationContextProvider; } }
    public IOpenDocumentHelper OpenDocumentHelper { get { return _openDocumentHelper; } }

    public void NavigateToFile(FileEntryViewModel fileEntry, Span? span) {
      // Using "Post" is important: it allows the newly opened document to
      // receive the focus.
      SynchronizationContextProvider.UIContext.Post(() => 
        OpenDocumentHelper.OpenDocument(fileEntry.Path, _ => span));
    }

    public void NavigateToDirectory(DirectoryEntryViewModel directoryEntry) {
      // The use of "Post" is significant, as it prevents the message from
      // bubbling up thus preventing the newly opened document to receive
      // the focus.
      SynchronizationContextProvider.UIContext.Post(() => {
        _control.ViewModel.SelectDirectory(directoryEntry,
                                  _control.FileTreeView,
                                  () => _control.SwallowsRequestBringIntoView(false),
                                  () => _control.SwallowsRequestBringIntoView(true));
      });
    }
  }
}