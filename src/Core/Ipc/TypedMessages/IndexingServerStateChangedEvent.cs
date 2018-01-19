using System;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class IndexingServerStateChangedEvent : TypedEvent {
    [ProtoMember(1)]
    public bool Paused { get; set; }
    [ProtoMember(2)]
    public bool PausedDueToError { get; set; }
    [ProtoMember(3)]
    public DateTime LastIndexUpdatedUtc { get; set; }
  }
}