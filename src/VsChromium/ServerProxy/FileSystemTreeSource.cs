// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Threads;

namespace VsChromium.ServerProxy {
  [Export(typeof(IFileSystemTreeSource))]
  public class FileSystemTreeSource : IFileSystemTreeSource {
    private readonly ITypedRequestProcessProxy _proxy;
    private readonly IDelayedOperationExecutor _delayedOperationExecutor;

    [ImportingConstructor]
    public FileSystemTreeSource(ITypedRequestProcessProxy proxy, IDelayedOperationExecutor delayedOperationExecutor) {
      _proxy = proxy;
      _delayedOperationExecutor = delayedOperationExecutor;
      _proxy.EventReceived += ProxyOnEventReceived;
    }

    public void Fetch() {
      FetchFileSystemTree();
    }

    public event Action<FileSystemTree> TreeReceived;
    public event Action<ErrorResponse> ErrorReceived;

    private void ProxyOnEventReceived(TypedEvent typedEvent) {
      var evt = typedEvent as FileSystemScanFinished;
      if (evt != null) {
        if (evt.Error == null) {
          _delayedOperationExecutor.Post(new DelayedOperation {
            Id = "FetchFileSystemTree",
            Delay = TimeSpan.FromSeconds(0.1),
            Action = FetchFileSystemTree
          });
        }
      }
    }

    private void FetchFileSystemTree() {
      _proxy.RunAsync(
        new GetFileSystemRequest(),
        typedResponse => {
          var response = (GetFileSystemResponse)typedResponse;
          OnTreeReceived(response.Tree);
        },
        OnErrorReceived);
    }

    protected virtual void OnErrorReceived(ErrorResponse obj) {
      ErrorReceived?.Invoke(obj);
    }

    protected virtual void OnTreeReceived(FileSystemTree obj) {
      TreeReceived?.Invoke(obj);
    }
  }
}