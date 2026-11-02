// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  public class GetFileExtractsRequestHandler : TypedMessageRequestHandler<GetFileExtractsRequest> {
    private readonly ISearchEngine _searchEngine;

    public GetFileExtractsRequestHandler(ISearchEngine searchEngine) {
      _searchEngine = searchEngine;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetFileExtractsRequest)typedRequest;
      var result = _searchEngine.GetFileExtracts(new FullPath(request.FileName), request.Positions, request.MaxExtractLength);
      return new GetFileExtractsResponse {
        FileName = request.FileName,
        FileExtracts = result.ToList()
      };
    }
  }
}
