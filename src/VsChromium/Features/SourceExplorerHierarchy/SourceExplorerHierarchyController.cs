// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Language.Intellisense;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.ServerProxy;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class SourceExplorerHierarchyController : ISourceExplorerHierarchyController {
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IFileSystemTreeSource _fileSystemTreeSource;
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IVsGlyphService _vsGlyphService;
    private readonly IOpenDocumentHelper _openDocumentHelper;
    private readonly IFileSystem _fileSystem;
    private readonly VsHierarchy _hierarchy;

    public SourceExplorerHierarchyController(
      ISynchronizationContextProvider synchronizationContextProvider,
      IFileSystemTreeSource fileSystemTreeSource,
      IVisualStudioPackageProvider visualStudioPackageProvider,
      IVsGlyphService vsGlyphService,
      IOpenDocumentHelper openDocumentHelper,
      IFileSystem fileSystem) {

      _synchronizationContextProvider = synchronizationContextProvider;
      _fileSystemTreeSource = fileSystemTreeSource;
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _vsGlyphService = vsGlyphService;
      _openDocumentHelper = openDocumentHelper;
      _fileSystem = fileSystem;
      _hierarchy = new VsHierarchy(
        visualStudioPackageProvider.Package.ServiceProvider,
        vsGlyphService);
    }

    private void HierarchyOnOpenDocument(string path) {
      if (!_fileSystem.FileExists(new FullPath(path)))
        return;
      _openDocumentHelper.OpenDocument(path, view => null);
    }

    public void Activate() {
      IVsSolutionEventsHandler vsSolutionEvents = new VsSolutionEventsHandler(_visualStudioPackageProvider);
      vsSolutionEvents.AfterOpenSolution += AfterOpenSolutionHandler;
      vsSolutionEvents.BeforeCloseSolution += BeforeCloseSolutionHandler;

      _fileSystemTreeSource.TreeReceived += OnTreeReceived;
      _fileSystemTreeSource.ErrorReceived += OnErrorReceived;

      // Force getting the tree and refreshing the ui hierarchy.
      _fileSystemTreeSource.Fetch();

      _hierarchy.OpenDocument += HierarchyOnOpenDocument;
      _hierarchy.SyncToActiveDocument += HierarchyOnSyncToActiveDocument;
    }

    private void HierarchyOnSyncToActiveDocument() {
      Logger.WrapActionInvocation(
        () => {
          var dte = _visualStudioPackageProvider.Package.DTE;
          var document = dte.ActiveDocument;
          if (document == null)
            return;
          var path = document.FullName;
          if (!PathHelpers.IsAbsolutePath(path))
            return;
          if (!PathHelpers.IsValidBclPath(path))
            return;

          //

          NodeViewModel node;
          if (_hierarchy.Nodes.RootNode.FindNodeByMoniker(path, out node)) {
            _hierarchy.SelectNode(node);
          }
        });
    }

    /// <summary>
    /// Note: This is executed on the UI thread.
    /// </summary>
    private void AfterOpenSolutionHandler() {
      _hierarchy.Refresh();
    }

    /// <summary>
    /// Note: This is executed on the UI thread.
    /// </summary>
    private void BeforeCloseSolutionHandler() {
      _hierarchy.Disconnect();
    }

    /// <summary>
    /// Note: This is executed on a background thred.
    /// </summary>
    private void OnTreeReceived(FileSystemTree fileSystemTree) {
      var nodes = new VsHierarchyNodes();
      SetupRootNode(nodes.RootNode);
      if (fileSystemTree != null) {
        foreach (var root in fileSystemTree.Root.Entries) {
          AddRootEntry(nodes, root);
        }
      }

      _synchronizationContextProvider.UIContext.Post(
        () => _hierarchy.SetNodes(nodes));
    }

    private void SetupRootNode(NodeViewModel root) {
      var name = "VS Chromium Projects";
      root.Name = name;
      root.Caption = name;
      root.ExpandByDefault = true;
      root.ImageIndex = _vsGlyphService.GetImageIndex(
        StandardGlyphGroup.GlyphClosedFolder,
        StandardGlyphItem.GlyphItemPublic);
      root.OpenFolderImageIndex = _vsGlyphService.GetImageIndex(
        StandardGlyphGroup.GlyphOpenFolder,
        StandardGlyphItem.GlyphItemPublic);
    }

    private void AddRootEntry(VsHierarchyNodes nodes, FileSystemEntry root) {
      AddNodeForEntry(nodes, root, null);
    }

    private void AddNodeForEntry(VsHierarchyNodes nodes, FileSystemEntry entry, NodeViewModel parent) {
      var directoryEntry = entry as DirectoryEntry;
      var node = directoryEntry != null
        ? (NodeViewModel)new DirectoryNodeViewModel()
        : (NodeViewModel)new FileNodeViewModel();

      node.Caption = entry.Name;
      node.Name = entry.Name;
      node.Path =
        (parent == null ? entry.Name : PathHelpers.CombinePaths(parent.Path, entry.Name));
      node.ExpandByDefault = (parent == null);
      node.IsExpanded = (parent == null);
      if (directoryEntry != null) {
        node.ImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphClosedFolder,
          StandardGlyphItem.GlyphItemPublic);
        node.OpenFolderImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphOpenFolder,
          StandardGlyphItem.GlyphItemPublic);

        foreach (var child in directoryEntry.Entries) {
          AddNodeForEntry(nodes, child, node);
        }
      } else {
        node.ImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphCSharpFile,
          StandardGlyphItem.GlyphItemPublic);
      }

      nodes.AddNode(node, parent);
    }

    private void OnErrorReceived(ErrorResponse errorResponse) {
      // TODO(rpaquay)
    }
  }
}