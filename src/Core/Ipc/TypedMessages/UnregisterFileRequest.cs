using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class UnregisterFileRequest : TypedRequest {
    [ProtoMember(1)]
    public string FileName { get; set; }
  }
}