using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class IndexingStateChanged : TypedEvent {
    public bool Paused { get; set; }
  }
}