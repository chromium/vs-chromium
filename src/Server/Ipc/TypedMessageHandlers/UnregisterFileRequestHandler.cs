using System.ComponentModel.Composition;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class UnregisterFileRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;

    [ImportingConstructor]
    public UnregisterFileRequestHandler(IFileSystemSnapshotManager snapshotManager) {
      _snapshotManager = snapshotManager;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      _snapshotManager.UnregisterFile(new FullPath(((UnregisterFileRequest)typedRequest).FileName));

      return new DoneResponse {
        Info = "processing..."
      };
    }
  }
}