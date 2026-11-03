// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Protocol.Generated;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  /// <summary>
  /// An entry of the tree of search results displayed by the Code Search tool
  /// window (see <see cref="SearchResultTreeBuilder"/>).
  /// </summary>
  public abstract class SearchResultEntry {
    protected SearchResultEntry(string name) {
      Name = name;
    }

    /// <summary>
    /// The name of the entry: the root path for a project, the path relative
    /// to the parent directory for a file.
    /// </summary>
    public string Name { get; }
  }

  /// <summary>
  /// A directory of the search results tree: the root (no name) or a project
  /// (named after the absolute root path of the project).
  /// </summary>
  public sealed class SearchResultDirectoryEntry : SearchResultEntry {
    public SearchResultDirectoryEntry(string name, IList<SearchResultEntry> entries)
      : base(name) {
      Entries = entries;
    }

    public IList<SearchResultEntry> Entries { get; }
  }

  /// <summary>
  /// A file of the search results tree, with the spans of the matches (code
  /// search) or no spans (path search).
  /// </summary>
  public sealed class SearchResultFileEntry : SearchResultEntry {
    public SearchResultFileEntry(string name, IList<TextSpan> spans)
      : base(name) {
      Spans = spans;
    }

    /// <summary>
    /// The spans of the matches in the file, <c>null</c> for a path search.
    /// </summary>
    public IList<TextSpan> Spans { get; }
  }

  /// <summary>
  /// Builds the tree displayed by the Code Search tool window from the flat
  /// search results of the server: a root containing one directory per
  /// project (named after the root path of the project, in the order of the
  /// server, i.e. sorted), each containing the matching files (named after
  /// their path relative to the project root, in the order of the server, i.e.
  /// sorted by relative path). This is the tree that the typed messages
  /// protocol used to return.
  /// </summary>
  public static class SearchResultTreeBuilder {
    public static SearchResultDirectoryEntry FromSearchCode(SearchCodeResult result) {
      return new SearchResultDirectoryEntry(null, (result.Projects ?? new List<ProjectMatches>())
        .Select(project => (SearchResultEntry)new SearchResultDirectoryEntry(
          project.RootPath,
          (project.Files ?? new List<FileMatches>())
          .Select(file => (SearchResultEntry)new SearchResultFileEntry(
            file.RelativePath,
            file.Matches ?? new List<TextSpan>()))
          .ToList()))
        .ToList());
    }

    public static SearchResultDirectoryEntry FromSearchPaths(SearchPathsResult result) {
      return new SearchResultDirectoryEntry(null, (result.Projects ?? new List<ProjectFiles>())
        .Select(project => (SearchResultEntry)new SearchResultDirectoryEntry(
          project.RootPath,
          (project.RelativePaths ?? new List<string>())
          .Select(path => (SearchResultEntry)new SearchResultFileEntry(path, null))
          .ToList()))
        .ToList());
    }
  }
}
