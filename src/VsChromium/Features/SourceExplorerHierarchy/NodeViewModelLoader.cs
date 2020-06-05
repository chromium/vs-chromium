// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.ServerProxy;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class NodeViewModelLoader : INodeViewModelLoader {
    private readonly ITypedRequestProcessProxy _typedRequestProcessProxy;

    public NodeViewModelLoader(ITypedRequestProcessProxy typedRequestProcessProxy) {
      _typedRequestProcessProxy = typedRequestProcessProxy;
    }

    public DirectoryEntry LoadChildren(DirectoryNodeViewModel node) {
      var tcs = new TaskCompletionSource<DirectoryEntry>();

      var request = new GetDirectoryEntriesRequest {
        ProjectPath = node.GetProjectPath().Value,
        DirectoryRelativePath = node.RelativePath
      };
      _typedRequestProcessProxy.RunUnbufferedAsync(request, RunAsyncOptions.Default,
        response => { LoadChildrenCallback(tcs, response); },
        response => { LoadChildrenErrorCallback(tcs, response); });

      return tcs.Task.Result;
    }

    public List<LoadChildrenResult> LoadChildrenMultiple(
      RootNodeViewModel projectNode, ICollection<DirectoryNodeViewModel> nodes) {
      var tcs = new TaskCompletionSource<List<LoadChildrenResult>>();

      var request = new GetDirectoryEntriesMultipleRequest {
        ProjectPath = projectNode.GetProjectPath().Value,
        RelativePathList = nodes.Select(x => x.RelativePath).ToList()
      };
      _typedRequestProcessProxy.RunUnbufferedAsync(request, RunAsyncOptions.Default,
        response => { LoadChildrenMultipleCallback(tcs, nodes, response); },
        response => { LoadChildrenMultipleErrorCallback(tcs, response); });

      return tcs.Task.Result;
    }

    private void LoadChildrenCallback(TaskCompletionSource<DirectoryEntry> tcs, TypedResponse typedResponse) {
      try {
        var response = (GetDirectoryEntriesResponse) typedResponse;
        tcs.TrySetResult(response.DirectoryEntry);
      }
      catch (Exception e) {
        tcs.TrySetException(e);
      }
    }

    private void LoadChildrenErrorCallback(TaskCompletionSource<DirectoryEntry> tcs, ErrorResponse response) {
      tcs.TrySetException(response.CreateException());
    }

    private void LoadChildrenMultipleCallback(TaskCompletionSource<List<LoadChildrenResult>> tcs,
      IEnumerable<DirectoryNodeViewModel> nodes, TypedResponse typedResponse) {
      try {
        var response = (GetDirectoryEntriesMultipleResponse) typedResponse;

        // Note: # of entries in response matches # of nodes in the request
        using (var e = nodes.GetEnumerator()) {
          var result = response.DirectoryEntries.Select(entry => {
            e.MoveNext();
            return new LoadChildrenResult(e.Current, entry.HasValue ? entry.Value : null);
          }).ToList();
          tcs.TrySetResult(result);
        }
      }
      catch (Exception e) {
        tcs.TrySetException(e);
      }
    }

    private void LoadChildrenMultipleErrorCallback(TaskCompletionSource<List<LoadChildrenResult>> tcs,
      ErrorResponse response) {
      tcs.TrySetException(response.CreateException());
    }
  }
}