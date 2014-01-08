using System.ComponentModel.Composition;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystem;

namespace VsChromiumServer.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class RemoveFileNameRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemProcessor _processor;

    [ImportingConstructor]
    public RemoveFileNameRequestHandler(IFileSystemProcessor processor) {
      _processor = processor;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      _processor.RemoveFile(((RemoveFileNameRequest)typedRequest).FileName);

      return new DoneResponse {
        Info = "processing..."
      };
    }
  }
}