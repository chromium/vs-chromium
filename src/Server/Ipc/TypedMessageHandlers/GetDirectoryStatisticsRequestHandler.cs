// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetDirectoryStatisticsRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemProcessor _processor;

    [ImportingConstructor]
    public GetDirectoryStatisticsRequestHandler(IFileSystemProcessor processor) {
      _processor = processor;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDirectoryStatisticsRequest)typedRequest;

      //var directoryname = FileSystemNameFactoryExtensions.GetProjectFileName(_fileSystemNameFactory, _projectDiscovery, path);
      //if (filename == null)
      //  return Enumerable.Empty<FileExtract>();

      //TODO(rpaquay)
      return new GetDirectoryStatisticsResponse {
        DirectoryCount = 0,
        FileCount =  0,
        IndexedFileCount = 0,
        TotalIndexedFileSize = 0
      };
    }
  }
}
