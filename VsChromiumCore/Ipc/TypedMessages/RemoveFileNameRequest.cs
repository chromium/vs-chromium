using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  public class RemoveFileNameRequest : TypedRequest {
    [ProtoMember(1)]
    public string FileName { get; set; }
  }
}