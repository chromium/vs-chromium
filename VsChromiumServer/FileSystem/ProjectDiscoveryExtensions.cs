using VsChromiumServer.FileSystemNames;
using VsChromiumServer.Projects;

namespace VsChromiumServer.FileSystem {
  public static class ProjectDiscoveryExtensions {
    /// <summary>
    /// Returns the absolute path of the project containing |filename|.
    /// Returns |null| if |filename| is not located within a local project directory.
    /// </summary>
    public static string GetProjectPath(this IProjectDiscovery projectDiscovery, string filename) {
      var project = projectDiscovery.GetProject(filename);
      if (project == null)
        return null;
      return project.RootPath;
    }

    public static bool IsFileSearchable(this IProjectDiscovery projectDiscovery, FileName filename) {
      var project = projectDiscovery.GetProjectFromRootPath(filename.GetProjectRoot().Name);
      if (project == null)
        return false;
      return project.SearchableFilesFilter.Include(filename.GetRelativePathFromProjectRoot());
    }
  }
}