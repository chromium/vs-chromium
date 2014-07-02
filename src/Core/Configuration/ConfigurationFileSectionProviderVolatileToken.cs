using System.Collections.Concurrent;
using System.Linq;
using VsChromium.Core.FileNames;
using VsChromium.Server.Projects;

namespace VsChromium.Core.Configuration {
  public class ConfigurationFileSectionProviderVolatileToken : IVolatileToken {
    private readonly ConcurrentDictionary<FullPath, IVolatileToken> _files = new ConcurrentDictionary<FullPath, IVolatileToken>();

    public void AddFile(FullPath name) {
      _files.AddOrUpdate(name, x => new FileUpdateVolatileToken(x), (k, v) => v);
    }

    public bool IsCurrent {
      get { return _files.All(x => x.Value.IsCurrent); }
    }
  }
}