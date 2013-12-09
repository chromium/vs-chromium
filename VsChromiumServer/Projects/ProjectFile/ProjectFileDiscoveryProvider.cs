using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromiumCore.Configuration;
using VsChromiumCore.FileNames;
using VsChromiumCore.Linq;

namespace VsChromiumServer.Projects.ProjectFile {
  [Export(typeof(IProjectDiscoveryProvider))]
  public class ProjectFileDiscoveryProvider : IProjectDiscoveryProvider {
    private readonly FullPathNameSet<IProject> _knownProjectRootDirectories = new FullPathNameSet<IProject>();
    private readonly FullPathNameSet<object> _knownNonProjectDirectories = new FullPathNameSet<object>();
    private readonly object _lock = new object();

    public IProject GetProjectFromRootPath(string projectRootPath) {
      var name = new FullPathName(projectRootPath);
      lock (this._lock) {
        return this._knownProjectRootDirectories.Get(name);
      }
    }

    public IProject GetProject(string filename) {
      var name = new FullPathName(filename);
      lock (this._lock) {
        // Cache hit?
        var root = this._knownProjectRootDirectories
            .Where(x => name.StartsWith(x.Key))
            .OrderByDescending(x => x.Key.FullName.Length)
            .FirstOrDefault();
        if (root.Key != default(FullPathName)) {
          return root.Value;
        }

        // Negative cache hit?
        if (this._knownNonProjectDirectories.Contains(name.Parent)) {
          return null;
        }

        // Nope: compute all the way...
        return GetProjectWorker(name);
      }
    }

    public void ValidateCache() {
      lock (this._lock) {
        this._knownProjectRootDirectories.RemoveWhere(x => !x.DirectoryExists);
        this._knownNonProjectDirectories.RemoveWhere(x => !x.DirectoryExists);
      }
    }

    private IProject GetProjectWorker(FullPathName filepath) {
      var directory = filepath.Parent;
      if (directory.DirectoryExists) {
        var projectPath = EnumerateParents(filepath).FirstOrDefault(x => ContainsProjectFile(x));
        if (projectPath != default(FullPathName)) {
          var project = CreateProject(projectPath);
          this._knownProjectRootDirectories.Add(projectPath, project);
          return project;
        }
      }

      // No one in the parent chain is a Chromium directory.
      EnumerateParents(filepath).ForAll(x => this._knownNonProjectDirectories.Add(x, null));
      return null;
    }

    private IProject CreateProject(FullPathName rootPath) {
      var fileWithSections = new FileWithSections(rootPath.Combine(ConfigurationFilenames.ProjectFileNameDetection).FullName);
      var configurationProvider = new FileWithSectionConfigurationProvider(fileWithSections);
      return new ProjectFileProject(configurationProvider, rootPath);
    }

    private static IEnumerable<FullPathName> EnumerateParents(FullPathName path) {
      var directory = path.Parent;
      for (var parent = directory; parent != default(FullPathName); parent = parent.Parent) {
        yield return parent;
      }
    }

    public static bool ContainsProjectFile(FullPathName path) {
      return path.Combine(ConfigurationFilenames.ProjectFileNameDetection).FileExists;
    }
  }
}