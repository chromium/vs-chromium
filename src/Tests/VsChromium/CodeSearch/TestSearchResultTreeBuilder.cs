// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Features.SourceExplorerHierarchy;
using VsChromium.Features.ToolWindows.CodeSearch;
using VsChromium.Protocol.Generated;

namespace VsChromium.Tests.VsChromium.CodeSearch {
  [TestClass]
  public class TestSearchResultTreeBuilder {
    [TestMethod]
    public void SearchCodeResultIsGroupedByProject() {
      var result = new SearchCodeResult {
        Projects = {
          new ProjectMatches {
            RootPath = @"c:\src\a",
            Files = {
              new FileMatches {
                RelativePath = @"base\foo.cc",
                Matches = { new TextSpan { Position = 10, Length = 3 }, new TextSpan { Position = 20, Length = 3 } }
              },
              new FileMatches {
                RelativePath = "readme.txt",
                Matches = { new TextSpan { Position = 0, Length = 3 } }
              },
            }
          },
          new ProjectMatches {
            RootPath = @"c:\src\b",
            Files = {
              new FileMatches {
                RelativePath = @"x\y\z.h",
                Matches = { new TextSpan { Position = 5, Length = 1 } }
              },
            }
          },
        },
        HitCount = 4,
      };

      var root = SearchResultTreeBuilder.FromSearchCode(result);

      Assert.IsNull(root.Name);
      Assert.AreEqual(2, root.Entries.Count);

      var projectA = (SearchResultDirectoryEntry)root.Entries[0];
      Assert.AreEqual(@"c:\src\a", projectA.Name);
      Assert.AreEqual(2, projectA.Entries.Count);
      var fooFile = (SearchResultFileEntry)projectA.Entries[0];
      Assert.AreEqual(@"base\foo.cc", fooFile.Name);
      Assert.AreEqual(2, fooFile.Spans.Count);
      Assert.AreEqual(10, fooFile.Spans[0].Position);
      Assert.AreEqual(3, fooFile.Spans[0].Length);
      Assert.AreEqual(20, fooFile.Spans[1].Position);
      Assert.AreEqual("readme.txt", projectA.Entries[1].Name);

      var projectB = (SearchResultDirectoryEntry)root.Entries[1];
      Assert.AreEqual(@"c:\src\b", projectB.Name);
      Assert.AreEqual(1, projectB.Entries.Count);
      Assert.AreEqual(@"x\y\z.h", projectB.Entries[0].Name);
      Assert.AreEqual(1, ((SearchResultFileEntry)projectB.Entries[0]).Spans.Count);
    }

    [TestMethod]
    public void SearchPathsResultIsGroupedByProject() {
      var result = new SearchPathsResult {
        Projects = {
          new ProjectFiles {
            RootPath = @"c:\src\a",
            RelativePaths = { @"base\foo.cc", @"base\foo.h", "readme.txt" }
          },
        },
        HitCount = 3,
        TotalCount = 10,
      };

      var root = SearchResultTreeBuilder.FromSearchPaths(result);

      Assert.AreEqual(1, root.Entries.Count);
      var project = (SearchResultDirectoryEntry)root.Entries[0];
      Assert.AreEqual(@"c:\src\a", project.Name);
      CollectionAssert.AreEqual(
        new[] { @"base\foo.cc", @"base\foo.h", "readme.txt" },
        project.Entries.Select(x => x.Name).ToList());
      // Path search results have no spans (no children in the tree view).
      Assert.IsTrue(project.Entries.Cast<SearchResultFileEntry>().All(x => x.Spans == null));
    }

    [TestMethod]
    public void EmptyResultsHaveNoEntries() {
      Assert.AreEqual(0, SearchResultTreeBuilder.FromSearchCode(new SearchCodeResult()).Entries.Count);
      Assert.AreEqual(0, SearchResultTreeBuilder.FromSearchPaths(new SearchPathsResult()).Entries.Count);
      Assert.AreEqual(0, SearchResultTreeBuilder.FromSearchCode(new SearchCodeResult { Projects = null }).Entries.Count);
    }

    [TestMethod]
    public void DirectoryEntriesListDirectoriesFirst() {
      var entries = new DirectoryEntries {
        RelativePath = "base",
        Exists = true,
        DirectoryNames = { "a", "b" },
        FileNames = { "c.cc", "a.h" },
      };

      var children = entries.GetChildEntries();

      CollectionAssert.AreEqual(new[] { "a", "b", "c.cc", "a.h" }, children.Select(x => x.Name).ToList());
      CollectionAssert.AreEqual(new[] { true, true, false, false }, children.Select(x => x.IsDirectory).ToList());
      Assert.AreEqual(0, new DirectoryEntries { DirectoryNames = null, FileNames = null }.GetChildEntries().Count);
    }
  }
}
