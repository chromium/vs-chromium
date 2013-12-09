namespace VsChromiumServer.Projects {
  public interface IProject {
    string RootPath { get; }

    IDirectoryFilter DirectoryFilter { get; }
    IFileFilter FileFilter { get; }
    ISearchableFilesFilter SearchableFilesFilter { get; }
  }
}