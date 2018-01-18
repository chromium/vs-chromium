using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class IndexingStateChangedEvent : TypedEvent {
    public bool Paused { get; set; }
  }
}