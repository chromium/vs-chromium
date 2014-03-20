// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedEvents {
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
      _typedEventSender = typedEventSender;
      _fileSystemProcessor = fileSystemProcessor;
      _searchEngine = searchEngine;
    }

    public void RegisterEventHandlers() {
      _fileSystemProcessor.TreeComputing += FileSystemProcessorOnTreeComputing;
      _fileSystemProcessor.TreeComputed += FileSystemProcessorOnTreeComputed;

      _searchEngine.FilesLoading += SearchEngineOnFilesLoading;
      _searchEngine.FilesLoaded += SearchEngineOnFilesLoaded;
    }

    private void FileSystemProcessorOnTreeComputing(long operationId) {
      _typedEventSender.SendEventAsync(new FileSystemTreeComputing {
        OperationId = operationId
      });
    }

    private void FileSystemProcessorOnTreeComputed(long operationId, FileSystemTree oldTree, FileSystemTree newTree) {
      _typedEventSender.SendEventAsync(new FileSystemTreeComputed {
        OperationId = operationId,
        OldVersion = oldTree.Version,
        NewVersion = newTree.Version
      });
    }

    private void SearchEngineOnFilesLoading(long operationId) {
      _typedEventSender.SendEventAsync(new SearchEngineFilesLoading {
        OperationId = operationId
      });
    }

    private void SearchEngineOnFilesLoaded(long operationId) {
      _typedEventSender.SendEventAsync(new SearchEngineFilesLoaded {
        OperationId = operationId
      });
    }
  }
}
