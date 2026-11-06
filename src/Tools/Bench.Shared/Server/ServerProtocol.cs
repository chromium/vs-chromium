// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json.Nodes;

namespace VsChromium.Bench.Shared.Server;

/// <summary>A file of a search result: full path, path relative to its project, match positions.</summary>
public sealed record SearchResultFile(string FullPath, string RelativePath, IReadOnlyList<(int Position, int Length)> Positions);

/// <summary>A request of the JSON-RPC protocol: method and parameters.</summary>
public sealed record ServerRequest(string Method, JsonObject Params);

/// <summary>
/// Helpers building the requests and parsing the results of the JSON-RPC protocol of the server
/// (see protocol/*.tsp and protocol/README.md).
/// </summary>
public static class ServerProtocol {
  public static ServerRequest Initialize(string clientName = "VsChromium.Tests.EndToEnd") =>
    new("server/initialize", new JsonObject { ["clientName"] = clientName, ["protocolVersion"] = 1 });

  public static ServerRequest RegisterFile(string path) =>
    new("files/register", new JsonObject { ["path"] = path });

  public static ServerRequest UnregisterFile(string path) =>
    new("files/unregister", new JsonObject { ["path"] = path });

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

  public static ServerRequest PauseIndexing() =>
    new("index/pause", new JsonObject());

  public static ServerRequest ResumeIndexing() =>
    new("index/resume", new JsonObject());

  public static ServerRequest RefreshTree() =>
    new("tree/refresh", new JsonObject());

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

  public static ServerRequest GetIndexStatus() =>
    new("index/status", new JsonObject());

  /// <summary>"project/clearCache" for a project, "index/clearCache" for all the projects.</summary>
  public static ServerRequest ClearCache(string? projectPath = null) =>
    projectPath != null
      ? new("project/clearCache", new JsonObject { ["projectPath"] = projectPath })
      : new("index/clearCache", new JsonObject());

  public static ServerRequest RebuildProject(string projectPath) =>
    new("project/rebuild", new JsonObject { ["projectPath"] = projectPath });

  public static ServerRequest GetDirectorySizes(string projectPath, string relativePath = "") =>
    new("project/directorySizes", new JsonObject { ["projectPath"] = projectPath, ["relativePath"] = relativePath });

  public static ServerRequest MeasureDirectory(string projectPath, string relativePath, bool? restart = null) {
    var parameters = new JsonObject { ["projectPath"] = projectPath, ["relativePath"] = relativePath };
    if (restart != null) {
      parameters["restart"] = restart;
    }
    return new("project/measureDirectory", parameters);
  }

  public static ServerRequest CancelMeasure(string projectPath, string relativePath) =>
    new("project/cancelMeasure", new JsonObject { ["projectPath"] = projectPath, ["relativePath"] = relativePath });

  public static ServerRequest AddIgnorePattern(string projectPath, string section, string pattern) =>
    new("project/addIgnorePattern", new JsonObject {
      ["projectPath"] = projectPath,
      ["section"] = section,
      ["pattern"] = pattern,
    });

  public static ServerRequest SetProjectCacheEnabled(string projectPath, bool enabled) =>
    new("project/setOptions", new JsonObject { ["projectPath"] = projectPath, ["cacheEnabled"] = enabled });

  public static ServerRequest GetServerHistory(long? sinceUnixMs = null) {
    var parameters = new JsonObject();
    if (sinceUnixMs != null) {
      parameters["sinceUnixMs"] = sinceUnixMs;
    }
    return new("server/history", parameters);
  }

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
