using System.ComponentModel.Composition;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class UnregisterFileRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemProcessor _processor;

    [ImportingConstructor]
    public UnregisterFileRequestHandler(IFileSystemProcessor processor) {
      _processor = processor;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      _processor.UnregisterFile(new FullPath(((UnregisterFileRequest)typedRequest).FileName));

      return new DoneResponse {
        Info = "processing..."
      };
    }
  }
}