// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json.Nodes;

namespace VsChromium.Tests.EndToEnd.Server;

/// <summary>A file of a search result (flattened from the server's result tree).</summary>
public sealed record SearchResultFile(string FullPath, string RelativePath, IReadOnlyList<(int Position, int Length)> Positions);

/// <summary>Helpers building requests and parsing responses of the server JSON protocol.</summary>
public static class ServerProtocol {
  public static JsonObject RegisterFile(string path) =>
    new() { ["$type"] = "RegisterFileRequest", ["fileName"] = path };

  public static JsonObject SearchCode(string text, int maxResults = 10000, bool matchCase = false,
                                      bool matchWholeWord = false, bool regex = false, string? filePathPattern = null,
                                      bool useRe2Engine = true) =>
    new() {
      ["$type"] = "SearchCodeRequest",
      ["searchParams"] = new JsonObject {
        ["searchString"] = text,
        ["filePathPattern"] = filePathPattern,
        ["maxResults"] = maxResults,
        ["matchCase"] = matchCase,
        ["matchWholeWord"] = matchWholeWord,
        ["regex"] = regex,
        ["includeSymLinks"] = true,
        ["useRe2Engine"] = useRe2Engine,
      },
    };

  public static JsonObject SearchFilePaths(string pattern, int maxResults = 2000) =>
    new() {
      ["$type"] = "SearchFilePathsRequest",
      ["searchParams"] = new JsonObject { ["searchString"] = pattern, ["maxResults"] = maxResults, ["includeSymLinks"] = true },
    };

  public static JsonObject GetFileExtracts(string path, IEnumerable<(int Position, int Length)> positions, int maxExtractLength = 120) =>
    new() {
      ["$type"] = "GetFileExtractsRequest",
      ["fileName"] = path,
      ["positions"] = new JsonArray(positions.Select(p => (JsonNode)new JsonObject { ["position"] = p.Position, ["length"] = p.Length }).ToArray()),
      ["maxExtractLength"] = maxExtractLength,
    };

  public static JsonObject GetServerStatistics(bool forceGc = false) =>
    new() { ["$type"] = "GetServerStatisticsRequest", ["forceGarbageCollection"] = forceGc };

  public static JsonObject GetDatabaseStatistics() =>
    new() { ["$type"] = "GetDatabaseStatisticsRequest" };

  /// <summary>
  /// Flattens a search result tree (root -> one directory entry per project, named with the project
  /// root path -> files and directories with names relative to their parent).
  /// </summary>
  public static List<SearchResultFile> FlattenResults(JsonObject? root) {
    var files = new List<SearchResultFile>();
    foreach (var project in root?["entries"]?.AsArray() ?? []) {
      var projectObj = project!.AsObject();
      var projectRoot = (string)projectObj["name"]!;
      Visit(projectObj, projectRoot, "", files);
    }
    return files;
  }

  private static void Visit(JsonObject dir, string projectRoot, string relativeDir, List<SearchResultFile> files) {
    foreach (var entryNode in dir["entries"]?.AsArray() ?? []) {
      var entry = entryNode!.AsObject();
      var name = (string)entry["name"]!;
      var relativePath = relativeDir.Length == 0 ? name : Path.Combine(relativeDir, name);
      var type = (string?)entry["$type"];
      if (type is null or "DirectoryEntry") {
        Visit(entry, projectRoot, relativePath, files);
      } else {
        var positions = entry["data"]?["positions"]?.AsArray()
          .Select(p => (p!["position"]!.GetValue<int>(), p["length"]!.GetValue<int>()))
          .ToList() ?? [];
        files.Add(new SearchResultFile(Path.Combine(projectRoot, relativePath), relativePath, positions));
      }
    }
  }
}
