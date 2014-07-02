using System.Collections.Concurrent;
using System.Linq;
using VsChromium.Core.Caching;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  public class ConfigurationFileSectionProviderVolatileToken : IVolatileToken {
    private readonly IFileSystem _fileSystem;
    private readonly ConcurrentDictionary<FullPath, IVolatileToken> _files = new ConcurrentDictionary<FullPath, IVolatileToken>();

    public ConfigurationFileSectionProviderVolatileToken(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public void AddFile(FullPath name) {
      _files.AddOrUpdate(name, x => new FileUpdateVolatileToken(_fileSystem, x), (k, v) => v);
    }

    public bool IsCurrent {
      get { return _files.All(x => x.Value.IsCurrent); }
    }
  }
}