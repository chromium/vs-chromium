using System;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetProjectDetailsRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public GetProjectDetailsRequestHandler(IFileSystemSnapshotManager snapshotManager, ISearchEngine searchEngine) {
      _snapshotManager = snapshotManager;
      _searchEngine = searchEngine;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetProjectDetailsRequest) typedRequest;
      request.MaxFilesByExtensionDetailsCount = Math.Min(request.MaxFilesByExtensionDetailsCount, int.MaxValue);
      request.MaxLargeFilesDetailsCount = Math.Min(request.MaxLargeFilesDetailsCount, int.MaxValue);

      var projectPath = new FullPath(request.ProjectPath);
      var fileSystemSnapshot = _snapshotManager.CurrentSnapshot;
      var projectSnapshot = fileSystemSnapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.Equals(projectPath));
      if (projectSnapshot == null) {
        throw new RecoverableErrorException($"Project \"{request.ProjectPath}\" not found");
      }

      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      return new GetProjectDetailsResponse {
        ProjectDetails = CreateProjectDetails(database, projectSnapshot, request.MaxFilesByExtensionDetailsCount,
          request.MaxLargeFilesDetailsCount)
      };
    }

    public static ProjectDetails CreateProjectDetails(IFileDatabaseSnapshot database, ProjectRootSnapshot project,
      int maxFilesByExtensionDetailsCount, int maxLargeFilesDetailsCount) {
      return new ProjectDetails {
        RootPath = project.Project.RootPath.Value,

        DirectoryDetails = GetDirectoryDetailsRequestHandler.CreateDirectoryDetails(database, project,
          project.Directory,
          maxFilesByExtensionDetailsCount, maxLargeFilesDetailsCount),

        ConfigurationDetails = CreateProjectConfigurationDetails(project)
      };
    }

    public static ProjectConfigurationDetails CreateProjectConfigurationDetails(ProjectRootSnapshot project) {
      return new ProjectConfigurationDetails {
        IgnorePathsSection = CreateSectionDetails(project.Project.IgnorePathsConfiguration),
        IgnoreSearchableFilesSection = CreateSectionDetails(project.Project.IgnoreSearchableFilesConfiguration),
        IncludeSearchableFilesSection = CreateSectionDetails(project.Project.IncludeSearchableFilesConfiguration)
      };
    }

    private static ProjectConfigurationSectionDetails CreateSectionDetails(IConfigurationSectionContents section) {
      return new ProjectConfigurationSectionDetails {
        ContainingFilePath = section.ContainingFilePath.Value,
        Name = section.Name,
        Contents = section.Contents.Aggregate("", (acc, s1) => acc + "\r\n" + s1)
      };
    }
  }
}