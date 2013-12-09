namespace VsChromiumServer.Projects {
  public interface IProjectDiscoveryProvider {
    /// <summary>
    /// Returns the |IProject| corresponding to the project containing |filename|.
    /// Returns |null| if |filename| is not known to this project provider.
    /// </summary>
    IProject GetProject(string filename);

    /// <summary>
    /// Returns the |IProject| corresponding to the project root path |projectRootPath|.
    /// Returns |null| if |projectRootPath| is not known to this project provider.
    /// </summary>
    IProject GetProjectFromRootPath(string projectRootPath);

    /// <summary>
    /// Reset internal cache, usually called when something drastic happened on the file system.
    /// </summary>
    void ValidateCache();
  }
}