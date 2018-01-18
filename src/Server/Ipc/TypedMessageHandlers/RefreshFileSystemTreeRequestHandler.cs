using System.ComponentModel.Composition;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class RefreshFileSystemTreeRequestHandler : TypedMessageRequestHandler {
    private readonly IFileRegistrationTracker _fileRegistrationTracker;

    [ImportingConstructor]
    public RefreshFileSystemTreeRequestHandler(IFileRegistrationTracker fileRegistrationTracker) {
      _fileRegistrationTracker = fileRegistrationTracker;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      _fileRegistrationTracker.RefreshAsync();
      return new RefreshFileSystemTreeResponse();
    }
  }
}