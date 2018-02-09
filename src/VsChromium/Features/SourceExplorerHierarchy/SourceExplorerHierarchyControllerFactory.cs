// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Files;
using VsChromium.Features.IndexServerInfo;
using VsChromium.Package;
using VsChromium.ServerProxy;
using VsChromium.Settings;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.SourceExplorerHierarchy {
  [Export(typeof(ISourceExplorerHierarchyControllerFactory))]
  public class SourceExplorerHierarchyControllerFactory : ISourceExplorerHierarchyControllerFactory {
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IFileSystemTreeSource _fileSystemTreeSource;
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IVsGlyphService _vsGlyphService;
    private readonly IImageSourceFactory _imageSourceFactory;
    private readonly IOpenDocumentHelper _openDocumentHelper;
    private readonly IFileSystem _fileSystem;
    private readonly IClipboard _clipboard;
    private readonly IWindowsExplorer _windowsExplorer;
    private readonly IDispatchThreadServerRequestExecutor _dispatchThreadServerRequestExecutor;
    private readonly IEventBus _eventBus;
    private readonly IGlobalSettingsProvider _globalSettingsProvider;
    private readonly IDelayedOperationExecutor _delayedOperationExecutor;
    private readonly IDispatchThread _dispatchThread;
    private readonly IShowServerInfoService _showServerInfoService;

    [ImportingConstructor]
    public SourceExplorerHierarchyControllerFactory(
      ISynchronizationContextProvider synchronizationContextProvider,
      IFileSystemTreeSource fileSystemTreeSource,
      IVisualStudioPackageProvider visualStudioPackageProvider,
      IVsGlyphService vsGlyphService,
      IImageSourceFactory imageSourceFactory,
      IOpenDocumentHelper openDocumentHelper,
      IFileSystem fileSystem,
      IClipboard clipboard,
      IWindowsExplorer windowsExplorer,
      IDispatchThreadServerRequestExecutor dispatchThreadServerRequestExecutor,
      IEventBus eventBus,
      IGlobalSettingsProvider globalSettingsProvider,
      IDelayedOperationExecutor delayedOperationExecutor,
      IDispatchThread dispatchThread,
      IShowServerInfoService showServerInfoService) {
      _synchronizationContextProvider = synchronizationContextProvider;
      _fileSystemTreeSource = fileSystemTreeSource;
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _vsGlyphService = vsGlyphService;
      _imageSourceFactory = imageSourceFactory;
      _openDocumentHelper = openDocumentHelper;
      _fileSystem = fileSystem;
      _clipboard = clipboard;
      _windowsExplorer = windowsExplorer;
      _dispatchThreadServerRequestExecutor = dispatchThreadServerRequestExecutor;
      _eventBus = eventBus;
      _globalSettingsProvider = globalSettingsProvider;
      _delayedOperationExecutor = delayedOperationExecutor;
      _dispatchThread = dispatchThread;
      _showServerInfoService = showServerInfoService;
    }

    public ISourceExplorerHierarchyController CreateController() {
      return new SourceExplorerHierarchyController(
        _synchronizationContextProvider,
        _fileSystemTreeSource,
        _visualStudioPackageProvider,
        _vsGlyphService,
        _imageSourceFactory,
        _openDocumentHelper,
        _fileSystem,
        _clipboard,
        _windowsExplorer,
        _dispatchThreadServerRequestExecutor,
        _eventBus,
        _globalSettingsProvider,
        _delayedOperationExecutor,
        _dispatchThread,
        _showServerInfoService);
    }
  }
}