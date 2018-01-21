using System;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class IndexingServerStateChangedEvent : TypedEvent {
    [ProtoMember(1)]
    public IndexingServerStatus ServerStatus { get; set; }
    [ProtoMember(2)]
    public DateTime LastIndexUpdatedUtc { get; set; }
  }
}