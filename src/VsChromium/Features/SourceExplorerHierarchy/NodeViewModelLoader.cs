// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
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

    public Task<DirectoryEntry> LoadChildrenAsync(DirectoryNodeViewModel parentNode) {
      var tcs = new TaskCompletionSource<DirectoryEntry>();

      var path = parentNode.FullPath.Value;
      var relativePath = parentNode.RelativePath;
      //TODO: Hack!
      path = path.Substring(0, path.Length - relativePath.Length);

      var request = new GetDirectoryEntriesRequest {
        ProjectPath = path,
        DirectoryRelativePath = relativePath
      };
      _typedRequestProcessProxy.RunAsync(request,
        response => { LoadChildrenCallback(tcs, response); },
        response => { LoadChildrenErrorCallback(tcs, response); });
      return tcs.Task;
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
  }
}