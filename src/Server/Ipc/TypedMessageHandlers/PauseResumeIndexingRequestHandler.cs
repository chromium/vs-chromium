using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class PauseResumeIndexingRequestHandler : TypedMessageRequestHandler {
    private readonly IIndexingServer _indexingServer;

    [ImportingConstructor]
    public PauseResumeIndexingRequestHandler(IIndexingServer indexingServer) {
      _indexingServer = indexingServer;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      _indexingServer.TogglePausedRunning();
      return new PauseResumeIndexingResponse();
    }
  }
}