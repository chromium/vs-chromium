using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public abstract class FileSystemEntryInternal {
    public abstract FileSystemName FileSystemName { get; }
  }
}