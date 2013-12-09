// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystem;
using VsChromiumServer.Search;

namespace VsChromiumServer.Ipc.TypedEvents {
  [Export(typeof(ITypedEventForwarder))]
  public class TypedEventForwarder : ITypedEventForwarder {
    private readonly IFileSystemProcessor _fileSystemProcessor;
    private readonly ISearchEngine _searchEngine;
    private readonly ITypedEventSender _typedEventSender;

    [ImportingConstructor]
    public TypedEventForwarder(
        ITypedEventSender typedEventSender,
        IFileSystemProcessor fileSystemProcessor,
        ISearchEngine searchEngine) {
      this._typedEventSender = typedEventSender;
      this._fileSystemProcessor = fileSystemProcessor;
      this._searchEngine = searchEngine;
    }

    public void RegisterEventHandlers() {
      this._fileSystemProcessor.TreeComputing += FileSystemProcessorOnTreeComputing;
      this._fileSystemProcessor.TreeComputed += FileSystemProcessorOnTreeComputed;

      this._searchEngine.FilesLoading += SearchEngineOnFilesLoading;
      this._searchEngine.FilesLoaded += SearchEngineOnFilesLoaded;
    }

    private void FileSystemProcessorOnTreeComputing(long operationId) {
      this._typedEventSender.SendEventAsync(new FileSystemTreeComputing {
        OperationId = operationId
      });
    }

    private void FileSystemProcessorOnTreeComputed(long operationId, FileSystemTree oldTree, FileSystemTree newTree) {
      this._typedEventSender.SendEventAsync(new FileSystemTreeComputed {
        OperationId = operationId,
        OldVersion = oldTree.Version,
        NewVersion = newTree.Version
      });
    }

    private void SearchEngineOnFilesLoading(long operationId) {
      this._typedEventSender.SendEventAsync(new SearchEngineFilesLoading {
        OperationId = operationId
      });
    }

    private void SearchEngineOnFilesLoaded(long operationId) {
      this._typedEventSender.SendEventAsync(new SearchEngineFilesLoaded {
        OperationId = operationId
      });
    }
  }
}
