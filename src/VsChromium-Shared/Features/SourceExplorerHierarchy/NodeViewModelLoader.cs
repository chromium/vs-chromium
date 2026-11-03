// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VsChromium.Core.Collections;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class NodeViewModelLoader : INodeViewModelLoader {
    private readonly IServerClient _serverClient;

    public NodeViewModelLoader(IServerClient serverClient) {
      _serverClient = serverClient;
    }

    public DirectoryEntries LoadChildren(DirectoryNodeViewModel node) {
      // "fake" root node does not support loading children dynamically (they should have been set manually)
      if (string.IsNullOrEmpty(node.FullPathString)) {
        return null;
      }

      var parameters = new TreeEntriesParams {
        ProjectPath = node.GetProjectPath().Value,
        RelativePath = node.RelativePath ?? ""
      };
      var entries = RunSynchronously(client => client.TreeEntriesAsync(parameters));
      return entries.Exists ? entries : null;
    }

    public IList<LoadChildrenResult> LoadChildrenMultiple(
      RootNodeViewModel projectNode, ICollection<DirectoryNodeViewModel> nodes) {
      // "fake" root node does not support loading children dynamically (they should have been set manually)
      if (string.IsNullOrEmpty(projectNode.FullPathString)) {
        return ArrayUtilities.EmptyList<LoadChildrenResult>.Instance;
      }

      var parameters = new TreeEntriesMultipleParams {
        ProjectPath = projectNode.GetProjectPath().Value,
        RelativePaths = nodes.Select(x => x.RelativePath ?? "").ToList()
      };
      var response = RunSynchronously(client => client.TreeEntriesMultipleAsync(parameters));

      // Note: # of entries in response matches # of nodes in the request
      using (var e = nodes.GetEnumerator()) {
        return response.Directories.Select(entry => {
          e.MoveNext();
          return new LoadChildrenResult(e.Current, entry.Exists ? entry : null);
        }).ToList();
      }
    }

    /// <summary>
    /// Sends a request and waits for its response. The request runs on the
    /// thread pool (and its continuations too), so this can be called on any
    /// thread, including the UI thread, without deadlocking.
    /// </summary>
    private T RunSynchronously<T>(Func<ProtocolClient, Task<T>> request) {
      var task = Task.Run(async () => {
        var client = await _serverClient.GetProtocolClientAsync().ConfigureAwait(false);
        return await request(client).ConfigureAwait(false);
      });
      return task.Result;
    }
  }
}
