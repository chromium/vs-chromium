using System.Collections.Concurrent;
using System.Linq;
using VsChromium.Core.FileNames;
using VsChromium.Server.Projects;

namespace VsChromium.Core.Configuration {
  public class ConfigurationFileSectionProviderVolatileToken : IVolatileToken {
    private readonly ConcurrentDictionary<FullPathName, IVolatileToken> _files = new ConcurrentDictionary<FullPathName, IVolatileToken>();

    public void AddFile(FullPathName name) {
      _files.AddOrUpdate(name, x => new FileUpdateVolatileToken(x), (k, v) => v);
    }

    public bool IsCurrent {
      get { return _files.All(x => x.Value.IsCurrent); }
    }
  }
}