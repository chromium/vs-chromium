// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// The symbolic links of the projects by target: the final target of each file symbolic link
  /// and of each directory symbolic link or junction, and the paths of the projects that see it.
  /// The directory watcher reports the changes of a target under its real path only (and only if
  /// it is watched: inside a project, or by <see cref="SymLinkTargetWatcher"/>), not under the path
  /// of the link: <see cref="Expand"/> adds the changes of the link paths (a change of the target of
  /// a file link is a change of the link file; a change under the target of a directory link is a
  /// change of the same path under the link). Immutable.
  /// </summary>
  public sealed class SymLinkTargetMap {
    public static readonly SymLinkTargetMap Empty = new SymLinkTargetMap(
      new Dictionary<string, List<LinkPath>>(Comparer), new Dictionary<string, List<LinkPath>>(Comparer),
      new Dictionary<string, string>(Comparer), Array.Empty<FullPath>());

    private static StringComparer Comparer => PathHelpers.FileNameComparer;

    /// <summary>A path of a project that sees a target: the project root and the relative path of the link.</summary>
    public readonly record struct LinkPath(FullPath ProjectRoot, RelativePath RelativePath) {
      public FullPath Path => ProjectRoot.Combine(RelativePath);
    }

    private readonly Dictionary<string, List<LinkPath>> _fileTargets;
    private readonly Dictionary<string, List<LinkPath>> _directoryTargets;
    /// <summary>Link full path => target full path (to reuse the resolution of unchanged links).</summary>
    private readonly Dictionary<string, string> _targetsByLink;
    private readonly IReadOnlyCollection<FullPath> _projectRoots;

    private SymLinkTargetMap(Dictionary<string, List<LinkPath>> fileTargets, Dictionary<string, List<LinkPath>> directoryTargets,
                             Dictionary<string, string> targetsByLink, IReadOnlyCollection<FullPath> projectRoots) {
      _fileTargets = fileTargets;
      _directoryTargets = directoryTargets;
      _targetsByLink = targetsByLink;
      _projectRoots = projectRoots;
      _sortedLinks = targetsByLink.Keys.ToArray();
      Array.Sort(_sortedLinks, Comparer);
    }

    /// <summary>The links, sorted (prefix searches).</summary>
    private readonly string[] _sortedLinks;

    public int FileLinkCount => _fileTargets.Values.Sum(x => x.Count);
    public int DirectoryLinkCount => _directoryTargets.Values.Sum(x => x.Count);

    /// <summary>The final targets of the file links.</summary>
    public IEnumerable<string> FileTargets => _fileTargets.Keys;

    /// <summary>The final targets of the directory links.</summary>
    public IEnumerable<string> DirectoryTargets => _directoryTargets.Keys;

    /// <summary>
    /// The map of the links of <paramref name="snapshot"/>. The targets of the links of <paramref
    /// name="previous"/> are reused (no disk access) unless the link is in <paramref
    /// name="invalidatedLinks"/> (e.g. created, deleted or replaced since); the others are resolved
    /// by <paramref name="resolve"/> (link full path, true if the target can come from the
    /// resolution made by the scan).
    /// </summary>
    public static SymLinkTargetMap Build(FileSystemSnapshot snapshot, SymLinkTargetMap previous, ISet<string> invalidatedLinks,
                                         Func<FullPath, bool, string> resolve) {
      var fileTargets = new Dictionary<string, List<LinkPath>>(Comparer);
      var directoryTargets = new Dictionary<string, List<LinkPath>>(Comparer);
      var targetsByLink = new Dictionary<string, string>(Comparer);
      previous ??= Empty;

      string Target(FullPath link) {
        var key = link.Value;
        if ((invalidatedLinks == null || !invalidatedLinks.Contains(key)) && previous._targetsByLink.TryGetValue(key, out var known))
          return known;
        var target = resolve(link, invalidatedLinks == null || !invalidatedLinks.Contains(key));
        return target == null ? null : Normalize(target);
      }

      void Add(Dictionary<string, List<LinkPath>> map, string target, LinkPath link) {
        if (!map.TryGetValue(target, out var list)) {
          map.Add(target, list = new List<LinkPath>());
        }
        list.Add(link);
      }

      foreach (var projectRoot in snapshot.ProjectRoots) {
        var root = projectRoot.Project.RootPath;
        var stack = new Stack<DirectorySnapshot>();
        stack.Push(projectRoot.Directory);
        while (stack.Count > 0) {
          var directory = stack.Pop();
          if (directory.IsSymLink) {
            var path = directory.DirectoryName.FullPath;
            var target = Target(path);
            if (target != null) {
              targetsByLink[path.Value] = target;
              Add(directoryTargets, target, new LinkPath(root, directory.DirectoryName.RelativePath));
            }
          }
          foreach (var index in directory.SymLinkFileIndices) {
            var file = directory.ChildFiles[index];
            var path = file.FullPath;
            var target = Target(path);
            if (target != null) {
              targetsByLink[path.Value] = target;
              Add(fileTargets, target, new LinkPath(root, file.RelativePath));
            }
          }
          foreach (var child in directory.ChildDirectories.ToForeachEnum()) {
            stack.Push(child);
          }
        }
      }
      return new SymLinkTargetMap(fileTargets, directoryTargets, targetsByLink,
        snapshot.ProjectRoots.Select(x => x.Project.RootPath).ToList());
    }

    /// <summary>A path without trailing separator (the key of the maps).</summary>
    public static string Normalize(string path) {
      var trimmed = System.IO.Path.TrimEndingDirectorySeparator(path);
      return trimmed.Length == 0 ? path : trimmed;
    }

    /// <summary>True if <paramref name="path"/> is a link of the map.</summary>
    public bool IsLink(FullPath path) {
      return _targetsByLink.ContainsKey(Normalize(path.Value));
    }

    /// <summary>The links at or under <paramref name="path"/> (e.g. a replaced link, or a deleted directory).</summary>
    public IEnumerable<string> LinksAtOrUnder(FullPath path) {
      var prefix = Normalize(path.Value);
      // Note: The links starting with the prefix are contiguous in the sorted array.
      var index = Array.BinarySearch(_sortedLinks, prefix, Comparer);
      if (index < 0) {
        index = ~index;
      }
      for (; index < _sortedLinks.Length && _sortedLinks[index].StartsWith(prefix, PathHelpers.FileNameComparison); index++) {
        if (IsSameOrUnder(_sortedLinks[index], prefix)) {
          yield return _sortedLinks[index];
        }
      }
    }

    /// <summary>
    /// The changes of the link paths that see the path of <paramref name="change"/> (none if it is
    /// not a target or under a target).
    /// </summary>
    public IEnumerable<PathChangeEntry> Expand(PathChangeEntry change) {
      if (_fileTargets.Count == 0 && _directoryTargets.Count == 0)
        yield break;
      var path = Normalize(change.Path.Value);
      // A file link: its target changed (or was created or deleted): the link changed.
      if (_fileTargets.TryGetValue(path, out var fileLinks)) {
        foreach (var link in fileLinks) {
          yield return new PathChangeEntry(link.ProjectRoot, link.RelativePath, PathChangeKind.Changed, PathKind.File);
        }
      }
      // A directory link: the same change under each link to the target or to one of its ancestors.
      if (_directoryTargets.Count == 0)
        yield break;
      for (var ancestor = path; !string.IsNullOrEmpty(ancestor); ancestor = ParentOf(ancestor)) {
        if (!_directoryTargets.TryGetValue(ancestor, out var directoryLinks))
          continue;
        var suffix = path.Substring(ancestor.Length).TrimStart(PathHelpers.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        foreach (var link in directoryLinks) {
          if (suffix.Length == 0) {
            // The target itself (e.g. deleted, created): the link resolves to another directory.
            yield return new PathChangeEntry(link.ProjectRoot, link.RelativePath, change.ChangeKind == PathChangeKind.Changed
              ? PathChangeKind.Changed : change.ChangeKind, PathKind.Directory);
          } else {
            yield return new PathChangeEntry(link.ProjectRoot, link.RelativePath.CreateChild(suffix), change.ChangeKind, change.PathKind);
          }
        }
      }
    }

    /// <summary>
    /// <paramref name="changes"/> and the changes of the link paths that see them (each path once).
    /// </summary>
    public IList<PathChangeEntry> ExpandAll(IList<PathChangeEntry> changes) {
      if (_fileTargets.Count == 0 && _directoryTargets.Count == 0)
        return changes;
      List<PathChangeEntry> added = null;
      HashSet<string> seen = null;
      foreach (var change in changes) {
        foreach (var expanded in Expand(change)) {
          if (seen == null) {
            seen = new HashSet<string>(changes.Select(x => x.Path.Value), Comparer);
            added = new List<PathChangeEntry>();
          }
          if (seen.Add(expanded.Path.Value)) {
            added.Add(expanded);
          }
        }
      }
      return added == null ? changes : changes.Concat(added).ToList();
    }

    /// <summary>
    /// The directories to watch for the targets outside of the projects (watched already): the
    /// directory of the target of each file link (not recursive), the target of each directory link
    /// (recursive), without duplicates nor directories inside another watched directory. Only the
    /// targets of the links of the projects accepted by <paramref name="includeProject"/> (all if
    /// null). The targets on network volumes are excluded (not watchable).
    /// </summary>
    public IReadOnlyList<(FullPath Directory, bool Recursive)> GetTargetDirectoriesToWatch(Func<FullPath, bool> includeProject = null) {
      bool Included(List<LinkPath> links) => includeProject == null || links.Any(x => includeProject(x.ProjectRoot));
      var recursive = _directoryTargets
        .Where(x => Included(x.Value) && IsExternal(x.Key))
        .Select(x => x.Key)
        .OrderBy(x => x.Length)
        .ToList();
      // Not inside another recursive directory.
      var recursiveKept = new List<string>();
      foreach (var directory in recursive) {
        if (!IsInsideAny(directory, recursiveKept)) {
          recursiveKept.Add(directory);
        }
      }
      var nonRecursive = _fileTargets
        .Where(x => Included(x.Value) && IsExternal(x.Key))
        .Select(x => ParentOf(x.Key))
        .Where(x => !string.IsNullOrEmpty(x))
        .Distinct(Comparer)
        .Where(x => !IsInsideAny(x, recursiveKept))
        .ToList();
      return recursiveKept.Select(x => (new FullPath(x), true))
        .Concat(nonRecursive.Select(x => (new FullPath(x), false)))
        .ToList();
    }

    /// <summary>The statistics of the links of a project (see <see cref="SymLinkStatistics"/>).</summary>
    public SymLinkStatistics GetStatistics(FullPath projectRoot) {
      bool OfProject(LinkPath link) => link.ProjectRoot.Equals(projectRoot);
      var statistics = new SymLinkStatistics();
      foreach (var (targets, isFile) in new[] { (_fileTargets, true), (_directoryTargets, false) }) {
        foreach (var entry in targets) {
          var count = entry.Value.Count(OfProject);
          if (count == 0)
            continue;
          if (isFile) {
            statistics.FileLinkCount += count;
          } else {
            statistics.DirectoryLinkCount += count;
          }
          if (IsInsideAny(entry.Key, _projectRoots.Select(r => r.Value)))
            continue;
          if (IsOnNetworkVolume(entry.Key)) {
            statistics.NetworkTargetLinkCount += count;
          } else {
            statistics.ExternalTargetLinkCount += count;
          }
        }
      }
      statistics.ExternalTargetDirectories = GetTargetDirectoriesToWatch(x => x.Equals(projectRoot));
      return statistics;
    }

    /// <summary>Outside of the projects (not watched by their watchers) and not on a network volume.</summary>
    private bool IsExternal(string target) {
      return !IsInsideAny(target, _projectRoots.Select(r => r.Value)) && !IsOnNetworkVolume(target);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> NetworkVolumes =
      new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A UNC path, or a path of a network drive (cached by volume root).</summary>
    public static bool IsOnNetworkVolume(string path) {
      if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        return true;
      if (!OperatingSystem.IsWindows())
        return false;
      string root;
      try {
        root = System.IO.Path.GetPathRoot(path);
      }
      catch (ArgumentException) {
        return false;
      }
      if (string.IsNullOrEmpty(root))
        return false;
      return NetworkVolumes.GetOrAdd(root, r => {
        try {
          return new System.IO.DriveInfo(r).DriveType == System.IO.DriveType.Network;
        }
        catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException || e is ArgumentException) {
          return false;
        }
      });
    }

    private static bool IsInsideAny(string path, IEnumerable<string> directories) {
      return directories.Any(d => IsSameOrUnder(path, Normalize(d)));
    }

    private static bool IsSameOrUnder(string path, string directory) {
      if (path.Length == directory.Length)
        return Comparer.Equals(path, directory);
      return path.Length > directory.Length &&
             IsSeparator(path[directory.Length]) &&
             path.StartsWith(directory, PathHelpers.FileNameComparison);
    }

    private static bool IsSeparator(char c) => c == PathHelpers.DirectorySeparatorChar || c == System.IO.Path.AltDirectorySeparatorChar;

    private static string ParentOf(string path) {
      var index = path.LastIndexOfAny(new[] { PathHelpers.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar });
      if (index <= 0)
        return null;
      var parent = path.Substring(0, index);
      // "C:" (drive root): keep the separator (and no parent for "C:\").
      if (parent.EndsWith(':')) {
        parent += PathHelpers.DirectorySeparatorChar;
      }
      return parent.Length < path.Length ? parent : null;
    }
  }

  /// <summary>The symbolic links of a project (see <see cref="SymLinkTargetMap.GetStatistics"/>).</summary>
  public sealed class SymLinkStatistics {
    public int FileLinkCount { get; set; }
    /// <summary>Directory symbolic links and junctions.</summary>
    public int DirectoryLinkCount { get; set; }
    /// <summary>The links whose final target is outside of the project roots, on a local volume.</summary>
    public int ExternalTargetLinkCount { get; set; }
    /// <summary>The links whose final target is on a network volume (never watched).</summary>
    public int NetworkTargetLinkCount { get; set; }
    /// <summary>The directories to watch for the targets outside of the project roots.</summary>
    public IReadOnlyList<(FullPath Directory, bool Recursive)> ExternalTargetDirectories { get; set; } =
      Array.Empty<(FullPath, bool)>();
    /// <summary>The targets outside of the project roots are watched (option of the project).</summary>
    public bool ExternalTargetsWatched { get; set; }
    /// <summary>The watchers running for <see cref="ExternalTargetDirectories"/>.</summary>
    public int WatcherCount { get; set; }
  }
}
