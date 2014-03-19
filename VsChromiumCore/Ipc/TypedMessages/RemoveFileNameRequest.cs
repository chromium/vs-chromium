using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class RemoveFileNameRequest : TypedRequest {
    [ProtoMember(1)]
    public string FileName { get; set; }
  }
}