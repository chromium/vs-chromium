using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace VsChromiumServer.Projects {
  [Export(typeof(IProjectDiscovery))]
  public class ProjectDiscovery : IProjectDiscovery {
    private readonly IProjectDiscoveryProvider[] _providers;

    [ImportingConstructor]
    public ProjectDiscovery([ImportMany] IEnumerable<IProjectDiscoveryProvider> providers) {
      this._providers = providers.ToArray();
    }

    public IProject GetProject(string filename) {
      for (var i = 0; i < this._providers.Length; i++) {
        var project = this._providers[i].GetProject(filename);
        if (project != null)
          return project;
      }
      return null;
    }

    public IProject GetProjectFromRootPath(string projectRootPath) {
      for (var i = 0; i < this._providers.Length; i++) {
        var project = this._providers[i].GetProjectFromRootPath(projectRootPath);
        if (project != null)
          return project;
      }
      return null;
    }

    public void ValidateCache() {
      for (var i = 0; i < this._providers.Length; i++) {
        this._providers[i].ValidateCache();
      }
    }
  }
}