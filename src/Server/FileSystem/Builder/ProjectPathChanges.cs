using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystem.Builder {
  /// <summary>
  /// Map from RelativePath to PathChangeKind for a given project root path.
  /// Note: This class is thread safe
  /// </summary>
  public class ProjectPathChanges {
    private readonly FullPath _projectPath;
    private readonly Dictionary<RelativePath, PathChangeKind> _map;
    private readonly Lazy<Dictionary<RelativePath, List<RelativePath>>> _createdChildren;
    private readonly Lazy<Dictionary<RelativePath, List<RelativePath>> >_deletedChildren;

    public ProjectPathChanges(FullPath projectPath, IList<PathChangeEntry> entries) {
      _projectPath = projectPath;

      _map = entries
        .Where(x => x.BasePath.Equals(_projectPath))
        .Select(x => KeyValuePair.Create(x.RelativePath, x.ChangeKind))
        .ToDictionary(x => x.Key, x => x.Value);

      _createdChildren = new Lazy<Dictionary<RelativePath, List<RelativePath>>>(() => _map
        .Where(x => x.Value == PathChangeKind.Created)
        .GroupBy(x => x.Key.Parent)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList()));

      _deletedChildren = new Lazy<Dictionary<RelativePath, List<RelativePath>>>(() => _map
        .Where(x => x.Value == PathChangeKind.Deleted)
        .GroupBy(x => x.Key.Parent)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList()));
    }

    public PathChangeKind GetPathChangeKind(RelativePath path) {
      PathChangeKind result;
      if (!_map.TryGetValue(path, out result)) {
        result = PathChangeKind.None;
      }
      return result;
    }

    public bool IsDeleted(RelativePath path) {
      return GetPathChangeKind(path) == PathChangeKind.Deleted;
    }

    public bool IsCreated(RelativePath path) {
      return GetPathChangeKind(path) == PathChangeKind.Created;
    }

    public bool IsChanged(RelativePath path) {
      return GetPathChangeKind(path) == PathChangeKind.Changed;
    }

    public IList<RelativePath> GetCreatedEntries(RelativePath parentPath) {
      return _createdChildren.Value.GetValue(parentPath) ?? ArrayUtilities.EmptyList<RelativePath>.Instance;
    }

    public IList<RelativePath> GetDeletedEntries(RelativePath parentPath) {
      return _deletedChildren.Value.GetValue(parentPath) ?? ArrayUtilities.EmptyList<RelativePath>.Instance;
    }
  }
}