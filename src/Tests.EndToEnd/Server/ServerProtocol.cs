// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json.Nodes;

namespace VsChromium.Tests.EndToEnd.Server;

/// <summary>A file of a search result: full path, path relative to its project, match positions.</summary>
public sealed record SearchResultFile(string FullPath, string RelativePath, IReadOnlyList<(int Position, int Length)> Positions);

/// <summary>A request of the JSON-RPC protocol: method and parameters.</summary>
public sealed record ServerRequest(string Method, JsonObject Params);

/// <summary>
/// Helpers building the requests and parsing the results of the JSON-RPC protocol of the server
/// (see protocol/*.tsp and protocol/README.md).
/// </summary>
public static class ServerProtocol {
  public static ServerRequest Initialize() =>
    new("server/initialize", new JsonObject { ["clientName"] = "VsChromium.Tests.EndToEnd", ["protocolVersion"] = 1 });

  public static ServerRequest RegisterFile(string path) =>
    new("files/register", new JsonObject { ["path"] = path });

  public static ServerRequest SearchCode(string text, int maxResults = 10000, bool matchCase = false,
                                         bool matchWholeWord = false, bool regex = false, string? filePathPattern = null) =>
    new("search/code", new JsonObject {
      ["query"] = Query(text, matchCase, matchWholeWord, regex, filePathPattern),
      ["maxResults"] = maxResults,
    });

  public static ServerRequest SearchFilePaths(string pattern, int maxResults = 2000, bool regex = false) =>
    new("search/paths", new JsonObject {
      ["query"] = Query(pattern, matchCase: false, matchWholeWord: false, regex, filePathPattern: null),
      ["maxResults"] = maxResults,
    });

  private static JsonObject Query(string text, bool matchCase, bool matchWholeWord, bool regex, string? filePathPattern) {
    var query = new JsonObject {
      ["text"] = text,
      ["matchCase"] = matchCase,
      ["matchWholeWord"] = matchWholeWord,
      ["regex"] = regex,
      ["includeSymLinks"] = true,
    };
    if (filePathPattern != null) {
      query["filePathPattern"] = filePathPattern;
    }
    return query;
  }

  public static ServerRequest GetFileExtracts(string path, IEnumerable<(int Position, int Length)> positions, int maxExtractLength = 120) =>
    new("files/extracts", new JsonObject {
      ["path"] = path,
      ["spans"] = new JsonArray(positions.Select(p => (JsonNode)new JsonObject { ["position"] = p.Position, ["length"] = p.Length }).ToArray()),
      ["maxExtractLength"] = maxExtractLength,
    });

  public static ServerRequest GetServerStatistics(bool forceGc = false) =>
    new("server/statistics", new JsonObject { ["forceGarbageCollection"] = forceGc });

  public static ServerRequest SetProjectOptions(string projectPath, bool? trigramFiltersEnabled) {
    var parameters = new JsonObject { ["projectPath"] = projectPath };
    if (trigramFiltersEnabled != null) {
      parameters["trigramFiltersEnabled"] = trigramFiltersEnabled;
    }
    return new("project/setOptions", parameters);
  }

  public static ServerRequest GetProjectDetails(string projectPath) =>
    new("project/details", new JsonObject { ["projectPath"] = projectPath, ["maxFilesByExtensionDetailsCount"] = 0, ["maxLargeFilesDetailsCount"] = 0 });

  public static ServerRequest GetIndexStatistics() =>
    new("index/statistics", new JsonObject());

  /// <summary>The files of a "search/code" result (projects -> files -> matches).</summary>
  public static List<SearchResultFile> FlattenCodeResults(JsonObject result) {
    var files = new List<SearchResultFile>();
    foreach (var project in result["projects"]?.AsArray() ?? []) {
      var rootPath = (string)project!["rootPath"]!;
      foreach (var file in project["files"]?.AsArray() ?? []) {
        var relativePath = (string)file!["relativePath"]!;
        var positions = file["matches"]?.AsArray()
          .Select(p => (p!["position"]!.GetValue<int>(), p["length"]!.GetValue<int>()))
          .ToList() ?? [];
        files.Add(new SearchResultFile(Path.Combine(rootPath, relativePath), relativePath, positions));
      }
    }
    return files;
  }

  /// <summary>The files of a "search/paths" result (projects -> relative paths).</summary>
  public static List<SearchResultFile> FlattenPathResults(JsonObject result) {
    var files = new List<SearchResultFile>();
    foreach (var project in result["projects"]?.AsArray() ?? []) {
      var rootPath = (string)project!["rootPath"]!;
      foreach (var path in project["relativePaths"]?.AsArray() ?? []) {
        var relativePath = (string)path!;
        files.Add(new SearchResultFile(Path.Combine(rootPath, relativePath), relativePath, []));
      }
    }
    return files;
  }
}
