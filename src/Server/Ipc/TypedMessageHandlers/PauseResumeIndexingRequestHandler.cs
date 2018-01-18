using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class PauseResumeIndexingRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;

    [ImportingConstructor]
    public PauseResumeIndexingRequestHandler(IFileSystemSnapshotManager snapshotManager) {
      _snapshotManager = snapshotManager;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      switch (_snapshotManager.GetStatus().State) {
        case IndexingState.Running:
          _snapshotManager.Pause();
          break;
        case IndexingState.Paused:
          _snapshotManager.Resume();
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
      return new PauseResumeIndexingResponse();
    }
  }
}