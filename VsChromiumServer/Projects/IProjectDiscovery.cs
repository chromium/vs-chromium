namespace VsChromiumServer.Projects {
  public interface IProjectDiscovery {
    /// <summary>
    /// Returns the absolute path of the project containing |filename|.
    /// Returns |null| if |filename| is not located within a local project directory.
    /// </summary>
    IProject GetProject(string filename);

    IProject GetProjectFromRootPath(string projectRootPath);

    /// <summary>
    /// Reset internal cache, usually called when something drastic happened on the file system.
    /// </summary>
    void ValidateCache();
  }
}
