namespace VsChromiumServer.Projects {
  public static class ProjectDiscoveryProviderExtensions {
    /// <summary>
    /// Returns the absolute path of the project containing |filename|.
    /// Returns |null| if |filename| is not located within a local project directory.
    /// </summary>
    public static string GetProjectPath(this IProjectDiscoveryProvider provider, string filename) {
      var project = provider.GetProject(filename);
      if (project == null)
        return null;
      return project.RootPath;
    }
  }
}