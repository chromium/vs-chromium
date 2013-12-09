using VsChromiumCore.Configuration;
using VsChromiumCore.FileNames;

namespace VsChromiumServer.Projects.ProjectFile {
  internal class ProjectFileProject : IProject {
    private readonly FullPathName _rootPath;
    private readonly IDirectoryFilter _directoryFilter;
    private readonly IFileFilter _fileFilter;
    private readonly ISearchableFilesFilter _searchableFilesFilter;

    public ProjectFileProject(IConfigurationSectionProvider configurationSectionProvider, FullPathName rootPath) {
      this._rootPath = rootPath;
      this._directoryFilter = new DirectoryFilter(configurationSectionProvider);
      this._fileFilter = new FileFilter(configurationSectionProvider);
      this._searchableFilesFilter = new SearchableFilesFilter(configurationSectionProvider);
    }

    public string RootPath {
      get {
        return this._rootPath.FullName;
      }
    }

    public IDirectoryFilter DirectoryFilter {
      get {
        return this._directoryFilter;
      }
    }

    public IFileFilter FileFilter {
      get {
        return this._fileFilter;
      }
    }

    public ISearchableFilesFilter SearchableFilesFilter {
      get {
        return this._searchableFilesFilter;
      }
    }
  }
}