using System.ComponentModel.Composition;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class RefreshFileSystemTreeRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemProcessor _processor;

    [ImportingConstructor]
    public RefreshFileSystemTreeRequestHandler(IFileSystemProcessor processor) {
      _processor = processor;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      _processor.Refresh();
      return new RefreshFileSystemTreeResponse();
    }
  }
}