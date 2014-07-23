using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDirectoryStatisticsResponse : TypedResponse {
    [ProtoMember(1)]
    public int DirectoryCount { get; set; }
    [ProtoMember(2)]
    public int FileCount { get; set; }
    [ProtoMember(3)]
    public int IndexedFileCount { get; set; }
    [ProtoMember(4)]
    public long TotalIndexedFileSize { get; set; }
  }
}