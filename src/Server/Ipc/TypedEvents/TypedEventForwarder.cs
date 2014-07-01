// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Operations;
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
      _fileSystemProcessor.SnapshotComputing += FileSystemProcessorOnSnapshotComputing;
      _fileSystemProcessor.SnapshotComputed += FileSystemProcessorOnSnapshotComputed;

      _searchEngine.FilesLoading += SearchEngineOnFilesLoading;
      _searchEngine.FilesLoaded += SearchEngineOnFilesLoaded;
    }

    private void FileSystemProcessorOnSnapshotComputing(object sender, OperationInfo e) {
      _typedEventSender.SendEventAsync(new FileSystemTreeComputing {
        OperationId = e.OperationId
      });
    }

    private void FileSystemProcessorOnSnapshotComputed(object sender, SnapshotComputedResult e) {
      var fileSystemTreeComputed = new FileSystemTreeComputed {
        OperationId = e.OperationInfo.OperationId,
        Error = ErrorResponseHelper.CreateErrorResponse(e.Error)
      };

      if (e.PreviousSnapshot != null) {
        fileSystemTreeComputed.OldVersion = e.PreviousSnapshot.Version;
        fileSystemTreeComputed.NewVersion = e.NewSnapshot.Version;
      }

      _typedEventSender.SendEventAsync(fileSystemTreeComputed);
    }

    private void SearchEngineOnFilesLoading(object sender, OperationInfo args) {
      _typedEventSender.SendEventAsync(new SearchEngineFilesLoading {
        OperationId = args.OperationId
      });
    }

    private void SearchEngineOnFilesLoaded(object sender, FilesLoadedResult args) {
      _typedEventSender.SendEventAsync(new SearchEngineFilesLoaded {
        OperationId = args.OperationInfo.OperationId,
        Error = ErrorResponseHelper.CreateErrorResponse(args.Error)
      });
    }
  }
}
