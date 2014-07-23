using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDirectoryStatisticsRequest : TypedRequest {
    [ProtoMember(1)]
    public string DirectoryName { get; set; }
  }
}