using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.Projects {
  [Export(typeof(IProjectDiscovery))]
  public class ProjectDiscoveryWithCache : IProjectDiscovery {
    private readonly IRawProjectDiscovery _projectDiscovery;
    private readonly object _lock = new object();
    private readonly Dictionary<FullPathName, IProject> _filenameToProject = new Dictionary<FullPathName, IProject>();
    private readonly Dictionary<FullPathName, IProject> _projectPathToProject = new Dictionary<FullPathName, IProject>();

    [ImportingConstructor]
    public ProjectDiscoveryWithCache(IRawProjectDiscovery projectDiscovery) {
      _projectDiscovery = projectDiscovery;
    }

    public IProject GetProject(FullPathName filename) {
      IProject result;
      lock (_lock) {
        if (_filenameToProject.TryGetValue(filename, out result)) {
          return result;
        }
      }

      result = _projectDiscovery.GetProject(filename);

      lock (_lock) {
        _filenameToProject[filename] = result;
      }

      return result;
    }

    public IProject GetProjectFromRootPath(FullPathName projectRootPath) {
      IProject result;
      lock (_lock) {
        if (_projectPathToProject.TryGetValue(projectRootPath, out result)) {
          return result;
        }
      }

      result = _projectDiscovery.GetProjectFromRootPath(projectRootPath);

      lock (_lock) {
        _projectPathToProject[projectRootPath] = result;
      }

      return result;
    }

    public void ValidateCache() {
      lock (_lock) {
        _filenameToProject.Clear();
        _projectPathToProject.Clear();
      }
      _projectDiscovery.ValidateCache();
    }
  }
}