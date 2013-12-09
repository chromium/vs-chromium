using System.Collections.Generic;
using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  public class GetFileExtractsResponse : TypedResponse {
    public GetFileExtractsResponse() {
      this.FileExtracts = new List<FileExtract>();
    }

    [ProtoMember(1)]
    public string FileName { get; set; }

    [ProtoMember(2)]
    public List<FileExtract> FileExtracts { get; set; }
  }
}