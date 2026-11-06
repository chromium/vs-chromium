// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Tests.Server {
  /// <summary>
  /// <see cref="SymLinkTargetMap"/>: the changes of the targets of symbolic links mapped to the paths
  /// of the links.
  /// </summary>
  [TestClass]
  public class TestSymLinkTargetMap {
    private static readonly string Base = Path.Combine(Path.GetTempPath(), "symlink-map");
    private static readonly FullPath Root = new FullPath(Path.Combine(Base, "project"));
    private static string Outside(params string[] names) => Path.Combine(new[] { Base, "outside" }.Concat(names).ToArray());
    private static string Inside(params string[] names) => Path.Combine(new[] { Root.Value }.Concat(names).ToArray());

    // project
    //   src/link.cc -> outside/target.cc (file link)
    //   src/local.cc -> project/tools/real.cc (file link inside the project)
    //   tools/real.cc
    //   dirlink -> outside/linked (directory link)
    //     sub/a.cc
    private readonly Dictionary<string, string> _targets = new Dictionary<string, string>(PathHelpers.FileNameComparer) {
      [Inside("src", "link.cc")] = Outside("target.cc"),
      [Inside("src", "local.cc")] = Inside("tools", "real.cc"),
      [Inside("dirlink")] = Outside("linked"),
    };
    private int _resolveCount;

    private FileSystemSnapshot Snapshot() {
      var factory = new FileSystemNameFactory();
      var projectFile = Root.Combine(new RelativePath(ConfigurationFileNames.ProjectFileName));
      var none = new ConfigurationSectionContents(projectFile, ConfigurationSectionNames.SourceExplorerIgnore, Array.Empty<string>());
      var include = new ConfigurationSectionContents(projectFile, ConfigurationSectionNames.SearchableFilesInclude, new[] { "*" });
      var project = new Project(Root, none, none, include, new FileFilter(none), new DirectoryFilter(none),
        new SearchableFilesFilter(none, include), "hash");
      var root = factory.CreateAbsoluteDirectoryName(Root);
      var src = factory.CreateDirectoryName(root, "src");
      var tools = factory.CreateDirectoryName(root, "tools");
      var dirlink = factory.CreateDirectoryName(root, "dirlink", true);
      var sub = factory.CreateDirectoryName(dirlink, "sub");
      DirectorySnapshot Dir(DirectoryName name, bool isSymLink, DirectorySnapshot[] children, string[] files, int[] links) =>
        new DirectorySnapshot(new DirectoryData(name, isSymLink), children, files.Select(x => factory.CreateFileName(name, x)).ToList(),
          files.Select(_ => default(FileMetadata)).ToArray(), links);
      var tree = Dir(root, false, new[] {
        Dir(dirlink, true, new[] { Dir(sub, false, Array.Empty<DirectorySnapshot>(), new[] { "a.cc" }, null) }, Array.Empty<string>(), null),
        Dir(src, false, Array.Empty<DirectorySnapshot>(), new[] { "file.cc", "link.cc", "local.cc" }, new[] { 1, 2 }),
        Dir(tools, false, Array.Empty<DirectorySnapshot>(), new[] { "real.cc" }, null),
      }, Array.Empty<string>(), null);
      return new FileSystemSnapshot(1, new ReadOnlyCollection<ProjectRootSnapshot>(new[] { new ProjectRootSnapshot(project, tree) }));
    }

    private SymLinkTargetMap Build(SymLinkTargetMap previous = null, ISet<string> invalidated = null) {
      return SymLinkTargetMap.Build(Snapshot(), previous, invalidated, (path, allowCached) => {
        _resolveCount++;
        return _targets.TryGetValue(path.Value, out var target) ? target : null;
      });
    }

    private static PathChangeEntry Change(string fullPath, PathChangeKind kind, PathKind pathKind = PathKind.File) {
      var path = new FullPath(fullPath);
      return new PathChangeEntry(path.Parent.Value, new RelativePath(path.FileName), kind, pathKind);
    }

    private static List<string> Describe(IEnumerable<PathChangeEntry> changes) {
      return changes.Select(x => $"{x.ChangeKind} {x.PathKind} {x.Path.Value}").OrderBy(x => x).ToList();
    }

    [TestMethod]
    public void MapsTheLinksOfTheSnapshot() {
      var map = Build();
      Assert.AreEqual(2, map.FileLinkCount);
      Assert.AreEqual(1, map.DirectoryLinkCount);
      Assert.AreEqual(3, _resolveCount);
    }

    [TestMethod]
    public void ChangesOfFileTargetsAreChangesOfTheLinks() {
      var map = Build();
      CollectionAssert.AreEqual(new[] { $"Changed File {Inside("src", "link.cc")}" },
        Describe(map.Expand(Change(Outside("target.cc"), PathChangeKind.Changed))));
      // Deleted or created target: the link changed (dangling or not anymore).
      CollectionAssert.AreEqual(new[] { $"Changed File {Inside("src", "link.cc")}" },
        Describe(map.Expand(Change(Outside("target.cc"), PathChangeKind.Deleted))));
      // A target inside the project.
      CollectionAssert.AreEqual(new[] { $"Changed File {Inside("src", "local.cc")}" },
        Describe(map.Expand(Change(Inside("tools", "real.cc"), PathChangeKind.Changed))));
      Assert.AreEqual(0, map.Expand(Change(Outside("other.cc"), PathChangeKind.Changed)).Count());
    }

    [TestMethod]
    public void ChangesUnderDirectoryTargetsAreChangesUnderTheLinks() {
      var map = Build();
      CollectionAssert.AreEqual(new[] { $"Created File {Inside("dirlink", "sub", "new.cc")}" },
        Describe(map.Expand(Change(Outside("linked", "sub", "new.cc"), PathChangeKind.Created))));
      CollectionAssert.AreEqual(new[] { $"Deleted Directory {Inside("dirlink", "sub")}" },
        Describe(map.Expand(Change(Outside("linked", "sub"), PathChangeKind.Deleted, PathKind.Directory))));
      // The target itself.
      CollectionAssert.AreEqual(new[] { $"Deleted Directory {Inside("dirlink")}" },
        Describe(map.Expand(Change(Outside("linked"), PathChangeKind.Deleted, PathKind.Directory))));
      Assert.AreEqual(0, map.Expand(Change(Outside("linked2", "a.cc"), PathChangeKind.Created)).Count(), "Not a prefix");
    }

    [TestMethod]
    public void ExpandAllAddsEachLinkPathOnce() {
      var map = Build();
      var changes = new[] {
        Change(Outside("target.cc"), PathChangeKind.Changed),
        Change(Outside("target.cc"), PathChangeKind.Changed),
        Change(Inside("src", "link.cc"), PathChangeKind.Changed),
      };
      var expanded = map.ExpandAll(changes);
      Assert.AreEqual(3, expanded.Count, "The link path is already a change");
      var unrelated = new[] { Change(Inside("src", "file.cc"), PathChangeKind.Changed) };
      Assert.AreSame(unrelated, map.ExpandAll(unrelated));
    }

    [TestMethod]
    public void TargetsOfUnchangedLinksAreReused() {
      var map = Build();
      Assert.AreEqual(3, _resolveCount);
      Build(map);
      Assert.AreEqual(3, _resolveCount, "No new resolution");
      // A link replaced: resolved again.
      var invalidated = new HashSet<string>(map.LinksAtOrUnder(new FullPath(Inside("src", "link.cc"))), PathHelpers.FileNameComparer);
      CollectionAssert.AreEqual(new[] { Inside("src", "link.cc") }, invalidated.ToList());
      _targets[Inside("src", "link.cc")] = Outside("other.cc");
      var rebuilt = Build(map, invalidated);
      Assert.AreEqual(4, _resolveCount);
      Assert.AreEqual(1, rebuilt.Expand(Change(Outside("other.cc"), PathChangeKind.Changed)).Count());
      Assert.AreEqual(0, rebuilt.Expand(Change(Outside("target.cc"), PathChangeKind.Changed)).Count());
      // All the links under a directory.
      Assert.AreEqual(2, map.LinksAtOrUnder(new FullPath(Inside("src"))).Count());
      Assert.AreEqual(3, map.LinksAtOrUnder(Root).Count());
    }

    [TestMethod]
    public void TargetDirectoriesOutsideOfTheProjectsAreWatched() {
      var map = Build();
      var directories = map.GetTargetDirectoriesToWatch().Select(x => $"{x.Directory.Value} {x.Recursive}").OrderBy(x => x).ToList();
      // The target of the directory link (recursive), the directory of the target of the file link
      // outside of the project (not recursive); not the target inside the project (watched).
      CollectionAssert.AreEqual(new[] { $"{Outside()} False", $"{Outside("linked")} True" }, directories);
    }
  }
}
