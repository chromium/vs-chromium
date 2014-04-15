using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public class FileEntryInternal : FileSystemEntryInternal {
    private readonly FileName _name;

    public FileEntryInternal(FileName name) {
      _name = name;
    }

    public FileName Name { get { return _name; } }
    public override FileSystemName FileSystemName { get { return _name; } }
  }
}