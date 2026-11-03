// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VsChromium.Core.Logging;
using VsChromium.Protocol.Generated;
using VsChromium.Threads;

namespace VsChromium.ServerProxy {
  [Export(typeof(IFileSystemTreeSource))]
  public class FileSystemTreeSource : IFileSystemTreeSource {
    private readonly IServerClient _serverClient;
    private readonly IDelayedOperationExecutor _delayedOperationExecutor;

    [ImportingConstructor]
    public FileSystemTreeSource(IServerClient serverClient, IDelayedOperationExecutor delayedOperationExecutor) {
      _serverClient = serverClient;
      _delayedOperationExecutor = delayedOperationExecutor;
      _serverClient.IndexScanFinished += ServerClientOnIndexScanFinished;
    }

    public void Fetch() {
      FetchFileSystemTree();
    }

    public event Action<FileSystemTree> TreeReceived;
    public event Action<ServerError> ErrorReceived;

    private void ServerClientOnIndexScanFinished(ScanFinishedParams parameters) {
      if (parameters.Error == null) {
        _delayedOperationExecutor.Post(new DelayedOperation {
          Id = "FetchFileSystemTree",
          Delay = TimeSpan.FromSeconds(0.1),
          Action = FetchFileSystemTree
        });
      }
    }

    private void FetchFileSystemTree() {
      // Runs on the thread pool, the events are raised on the thread pool.
      _ = Task.Run(() => FetchFileSystemTreeAsync());
    }

    private async Task FetchFileSystemTreeAsync() {
      FileSystemTree tree;
      try {
        var client = await _serverClient.GetProtocolClientAsync().ConfigureAwait(false);
        tree = await client.TreeGetAsync(new TreeParams()).ConfigureAwait(false);
      }
      catch (Exception e) {
        Logger.LogError(e, "Error fetching the file system tree from the server.");
        OnErrorReceived(ServerError.FromException(e));
        return;
      }
      OnTreeReceived(tree);
    }

    protected virtual void OnTreeReceived(FileSystemTree obj) {
      Logger.WrapActionInvocation(() => TreeReceived?.Invoke(obj));
    }

    protected virtual void OnErrorReceived(ServerError obj) {
      Logger.WrapActionInvocation(() => ErrorReceived?.Invoke(obj));
    }
  }
}
