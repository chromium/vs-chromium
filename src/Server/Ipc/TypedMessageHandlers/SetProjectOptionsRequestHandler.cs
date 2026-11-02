// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  public class SetProjectOptionsRequestHandler : TypedMessageRequestHandler<SetProjectOptionsRequest> {
    private readonly ISearchEngine _searchEngine;

    public SetProjectOptionsRequestHandler(ISearchEngine searchEngine) {
      _searchEngine = searchEngine;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (SetProjectOptionsRequest)typedRequest;
      if (string.IsNullOrEmpty(request.ProjectPath) || !PathHelpers.IsAbsolutePath(request.ProjectPath)) {
        throw new RecoverableErrorException($"Invalid project path \"{request.ProjectPath}\"");
      }
      _searchEngine.SetProjectOptions(new FullPath(request.ProjectPath), request.TrigramFiltersEnabled);
      return new DoneResponse {
        Info = "Project options updated"
      };
    }
  }
}
