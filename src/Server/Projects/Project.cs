using VsChromium.Core.Configuration;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.Projects {
  public class Project : IProject {
    private readonly FullPath _rootPath;
    private readonly IVolatileToken _configurationToken;
    private readonly IDirectoryFilter _directoryFilter;
    private readonly IFileFilter _fileFilter;
    private readonly ISearchableFilesFilter _searchableFilesFilter;

    public Project(IConfigurationSectionProvider configurationSectionProvider, FullPath rootPath) {
      _rootPath = rootPath;
      _configurationToken = configurationSectionProvider.WhenUpdated();
      _directoryFilter = new DirectoryFilter(configurationSectionProvider);
      _fileFilter = new FileFilter(configurationSectionProvider);
      _searchableFilesFilter = new SearchableFilesFilter(configurationSectionProvider);
    }

    public FullPath RootPath { get { return _rootPath; } }

    public IDirectoryFilter DirectoryFilter { get { return _directoryFilter; } }

    public IFileFilter FileFilter { get { return _fileFilter; } }

    public ISearchableFilesFilter SearchableFilesFilter { get { return _searchableFilesFilter; } }

    public bool IsOutdated { get { return !_configurationToken.IsCurrent; } }
  }
}