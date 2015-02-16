// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Language.Intellisense;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Package;
using VsChromium.ServerProxy;
using VsChromium.Threads;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class SourceExplorerHierarchyController : ISourceExplorerHierarchyController {
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IVsGlyphService _vsGlyphService;
    private readonly VsHierarchy _hierarchy;

    public SourceExplorerHierarchyController(
      ISynchronizationContextProvider synchronizationContextProvider,
      IFileSystemTreeSource fileSystemTreeSource,
      IVisualStudioPackageProvider visualStudioPackageProvider,
      IVsGlyphService vsGlyphService) {

      _synchronizationContextProvider = synchronizationContextProvider;
      _vsGlyphService = vsGlyphService;
      _hierarchy = new VsHierarchy(visualStudioPackageProvider.Package.ServiceProvider, vsGlyphService);

      IVsSolutionEventsHandler vsSolutionEvents = new VsSolutionEventsHandler(visualStudioPackageProvider);
      vsSolutionEvents.AfterOpenSolution += AfterOpenSolutionHandler;
      vsSolutionEvents.BeforeCloseSolution += BeforeCloseSolutionHandler;

      fileSystemTreeSource.TreeReceived += OnTreeReceived;
      fileSystemTreeSource.ErrorReceived += OnErrorReceived;

      // Force getting the tree and refreshing the ui hierarchy.
      fileSystemTreeSource.Fetch();
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
      var nodes = new VsHierarchyNodes(_hierarchy);
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
      var name = "VS Chromium Projects Source Explorer";
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
      var node = new NodeViewModel(_hierarchy) {
        Caption = entry.Name,
        Name = entry.Name,
        Moniker =
          (parent == null ? entry.Name : PathHelpers.CombinePaths(parent.Moniker, entry.Name)),
        ExpandByDefault = (parent == null),
        IsExpanded = (parent == null)
      };

      nodes.AddNode(node, parent);

      var directoryEntry = entry as DirectoryEntry;
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
      }
    }

    private void OnErrorReceived(ErrorResponse errorResponse) {
      // TODO(rpaquay)
    }
  }
}