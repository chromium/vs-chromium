// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Views;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class IncrementalHierarchyBuilderAggregate : IIncrementalHierarchyBuilder {
    private readonly INodeTemplateFactory _templateFactory;
    private readonly VsHierarchyAggregate _hierarchy;
    private readonly FileSystemTree _fileSystemTree;
    private readonly INodeViewModelLoader _nodeViewModelLoader;
    private readonly IImageSourceFactory _imageSourceFactory;

    public IncrementalHierarchyBuilderAggregate(
      INodeTemplateFactory nodeTemplateFactory,
      VsHierarchyAggregate hierarchy,
      FileSystemTree fileSystemTree,
      INodeViewModelLoader nodeViewModelLoader,
      IImageSourceFactory imageSourceFactory) {
      _templateFactory = nodeTemplateFactory;
      _hierarchy = hierarchy;
      _fileSystemTree = fileSystemTree;
      _nodeViewModelLoader = nodeViewModelLoader;
      _imageSourceFactory = imageSourceFactory;
    }

    public class RootEntry {
      public string RootPath { get; set; }
      public VsHierarchy Hierarchy { get; set; }
      public IncrementalHierarchyBuilder Builder { get; set; }
      public Func<int, ApplyChangesResult> ChangeApplier;
    }

    public Func<int, ApplyChangesResult> ComputeChangeApplier() {
      // Capture hierarchy version # for checking later that another
      // thread did not beat us.
      int hierarchyVersion = _hierarchy.Version;
      var oldHierarchies = _hierarchy.CloneHierarchyList();

      // For new hierarchies: create tree and compute nodes
      // For comon existing hierarchies: create tree and compute nodes
      // For removed hierarchies: create (empty) tree and compute nodes
      List<RootEntry> rootEntries = CreateRootEntries(oldHierarchies).ToList();

      rootEntries.ForAll(entry => { entry.ChangeApplier = entry.Builder.ComputeChangeApplier(); });

      return latestFileSystemTreeVersion => {
        // Apply if nobody beat us to is.
        if (_hierarchy.Version == hierarchyVersion) {
          Logger.LogInfo(
            "Updating VsHierarchyAggregate nodes for version {0} and file system tree version {1}",
            hierarchyVersion,
            _fileSystemTree.Version);
          rootEntries.ForAll(entry => {
            var result = entry.ChangeApplier(latestFileSystemTreeVersion);
            Invariants.Assert(result == ApplyChangesResult.Done);
          });
          var newHierarchies = rootEntries
            .Where(x => !x.Hierarchy.IsEmpty)
            .Select(x => x.Hierarchy)
            .ToList();
          _hierarchy.SetNewHierarchies(newHierarchies);
          return ApplyChangesResult.Done;
        }

        Logger.LogInfo(
          "VsHierarchyAggregate nodes have been updated concurrently, re-run or skip operation." +
          " Node verions={0}-{1}, Tree versions:{2}-{3}.",
          hierarchyVersion, _hierarchy.Version,
          _fileSystemTree.Version, latestFileSystemTreeVersion);

        // If the version of the hieararchy has changed since when we started,
        // another thread has passed us.  This means the decisions we made
        // about the changes to apply are incorrect at this point. So, we run
        // again if we are processing the latest known version of the file
        // system tree, as we should be the winner (eventually)
        if (_fileSystemTree.Version == latestFileSystemTreeVersion) {
          // Termination notes: We make this call only when the VsHierarchy
          // version changes between the time we capture it and this point.
          return ApplyChangesResult.Retry;
        }

        return ApplyChangesResult.Bail;

      };
    }

    private IEnumerable<RootEntry> CreateRootEntries(List<VsHierarchy> oldHierarchies) {
      foreach (var hierarchy in oldHierarchies) {
        var rootPath = GetHierarchyRootPath(hierarchy);

        var projectEntry = _fileSystemTree.Projects
          .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.RootPath, rootPath));
        if (projectEntry == null) {
          // Hierarchy should be deleted, as its root does not exist in new tree
          yield return new RootEntry {
            RootPath = rootPath,
            Hierarchy = hierarchy,
            Builder = new IncrementalHierarchyBuilder(_templateFactory, hierarchy, new FullPath(rootPath),
              _fileSystemTree.Version, _nodeViewModelLoader, _imageSourceFactory)
          };
        } else {
          // Hierarchy should be updated with new root entry
          yield return new RootEntry {
            RootPath = rootPath,
            Hierarchy = hierarchy,
            Builder = new IncrementalHierarchyBuilder(_templateFactory, hierarchy, new FullPath(rootPath),
              _fileSystemTree.Version, _nodeViewModelLoader, _imageSourceFactory)
          };
        }
      }

      // Look for new hierarchies
      foreach (var projectEntry in _fileSystemTree.Projects) {
        var rootPath = projectEntry.RootPath;
        var hierarchy = oldHierarchies
          .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(GetHierarchyRootPath(x), rootPath));
        if (hierarchy == null) {
          // A new hierarchy should be created
          var newHierarchy = _hierarchy.CreateHierarchy();
          yield return new RootEntry {
            RootPath = rootPath,
            Hierarchy = newHierarchy,
            Builder = new IncrementalHierarchyBuilder(_templateFactory, newHierarchy, new FullPath(rootPath),
              _fileSystemTree.Version, _nodeViewModelLoader, _imageSourceFactory)
          };
        }
      }
    }

    private static string GetHierarchyRootPath(VsHierarchy hierarchy) {
      var rootNode = hierarchy.Nodes.RootNode;
      if (PathHelpers.IsAbsolutePath(rootNode.Name)) {
        return rootNode.Name;
      } else {
        Invariants.Assert(rootNode.Children.Count > 0);
        return rootNode.Children[0].FullPathString;
      }
    }
  }
}