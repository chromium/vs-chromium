// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Files;
using VsChromium.Package;
using VsChromium.ServerProxy;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.SourceExplorerHierarchy {
  [Export(typeof(ISourceExplorerHierarchyControllerFactory))]
  public class SourceExplorerHierarchyControllerFactory : ISourceExplorerHierarchyControllerFactory {
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IFileSystemTreeSource _fileSystemTreeSource;
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IVsGlyphService _vsGlyphService;
    private readonly IOpenDocumentHelper _openDocumentHelper;
    private readonly IFileSystem _fileSystem;
    private readonly IClipboard _clipboard;

    [ImportingConstructor]
    public SourceExplorerHierarchyControllerFactory(
      ISynchronizationContextProvider synchronizationContextProvider,
      IFileSystemTreeSource fileSystemTreeSource,
      IVisualStudioPackageProvider visualStudioPackageProvider,
      IVsGlyphService vsGlyphService,
      IOpenDocumentHelper openDocumentHelper,
      IFileSystem fileSystem,
      IClipboard clipboard) {
      _synchronizationContextProvider = synchronizationContextProvider;
      _fileSystemTreeSource = fileSystemTreeSource;
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _vsGlyphService = vsGlyphService;
      _openDocumentHelper = openDocumentHelper;
      _fileSystem = fileSystem;
      _clipboard = clipboard;
    }

    public ISourceExplorerHierarchyController CreateController() {
      return new SourceExplorerHierarchyController(
        _synchronizationContextProvider,
        _fileSystemTreeSource,
        _visualStudioPackageProvider,
        _vsGlyphService,
        _openDocumentHelper,
        _fileSystem,
        _clipboard);
    }
  }
}