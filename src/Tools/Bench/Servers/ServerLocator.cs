// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace VsChromium.Bench.Servers;

/// <summary>A server executable found by <see cref="ServerLocator"/>.</summary>
public sealed record ServerCandidate(string Source, string Path, string Description);

/// <summary>
/// Finds the server, in this order: an explicit path (--server), a repository build next to the bench
/// or in the current directory (Binaries/Release/Server, then Debug), an installed VS Code extension,
/// GitHub releases of chromium/vs-chromium (downloaded and cached in the data directory).
/// </summary>
public sealed class ServerLocator(string dataDirectory, Action<string> log) {
  public const string Repository = "chromium/vs-chromium";

  public static string ExecutableName => OperatingSystem.IsWindows() ? "VsChromium.Server.exe" : "VsChromium.Server";

  /// <summary>The local candidates (no network access), in the order of preference.</summary>
  public List<ServerCandidate> FindLocal() {
    var result = new List<ServerCandidate>();
    foreach (var path in FindRepositoryBuilds()) {
      result.Add(new ServerCandidate("repo", path, $"repository build {path}"));
    }
    foreach (var path in FindVsCodeExtensions()) {
      result.Add(new ServerCandidate("vscode", path, $"VS Code extension {path}"));
    }
    foreach (var (tag, path) in FindCachedReleases()) {
      result.Add(new ServerCandidate("release:" + tag, path, $"release {tag} (downloaded) {path}"));
    }
    return result;
  }

  /// <summary>
  /// Resolves a --server value: a path (file or directory), "repo", "vscode", "release:latest",
  /// "release:&lt;tag&gt;", or "auto" (the first local candidate, then the latest release).
  /// </summary>
  public async Task<ServerCandidate> ResolveAsync(string source, CancellationToken cancellation) {
    switch (source) {
      case "auto": {
        var local = FindLocal();
        if (local.Count > 0) {
          return local[0];
        }
        log("No local server found: looking for the latest release on GitHub.");
        return await DownloadReleaseAsync("latest", cancellation);
      }
      case "repo":
        return FindLocal().FirstOrDefault(c => c.Source == "repo")
               ?? throw new ServerNotFoundException("No repository build found (Binaries/Release/Server or Binaries/Debug/Server next to the bench or in the current directory).");
      case "vscode":
        return FindLocal().FirstOrDefault(c => c.Source == "vscode")
               ?? throw new ServerNotFoundException("No installed VS Code extension with a server found (~/.vscode/extensions/chromium.vs-chromium-*/server).");
    }
    if (source.StartsWith("release:", StringComparison.Ordinal)) {
      return await DownloadReleaseAsync(source["release:".Length..], cancellation);
    }
    var file = Directory.Exists(source) ? System.IO.Path.Combine(source, ExecutableName) : source;
    if (!File.Exists(file)) {
      throw new ServerNotFoundException($"Server not found: \"{source}\".");
    }
    return new ServerCandidate(System.IO.Path.GetFullPath(file), System.IO.Path.GetFullPath(file), $"server {System.IO.Path.GetFullPath(file)}");
  }

  private static IEnumerable<string> FindRepositoryBuilds() {
    var starts = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory };
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var configuration in new[] { "Release", "Debug" }) {
      foreach (var start in starts) {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent) {
          var candidate = System.IO.Path.Combine(dir.FullName, "Binaries", configuration, "Server", ExecutableName);
          if (File.Exists(candidate) && seen.Add(candidate)) {
            yield return candidate;
            break;
          }
        }
      }
    }
  }

  private static IEnumerable<string> FindVsCodeExtensions() {
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    foreach (var root in new[] { ".vscode", ".vscode-insiders", ".vscode-server" }) {
      var extensions = System.IO.Path.Combine(home, root, "extensions");
      if (!Directory.Exists(extensions)) {
        continue;
      }
      var found = Directory.EnumerateDirectories(extensions, "chromium.vs-chromium-*")
        .Select(d => (Dir: d, Version: ParseVersion(System.IO.Path.GetFileName(d)["chromium.vs-chromium-".Length..])))
        .OrderByDescending(x => x.Version)
        .Select(x => System.IO.Path.Combine(x.Dir, "server", ExecutableName))
        .Where(File.Exists);
      foreach (var path in found) {
        yield return path;
      }
    }
  }

  private static Version ParseVersion(string text) {
    var core = text.Split('-')[0];
    return Version.TryParse(core, out var version) ? version : new Version(0, 0);
  }

  private string ReleasesDirectory => System.IO.Path.Combine(dataDirectory, "servers");

  private IEnumerable<(string Tag, string Path)> FindCachedReleases() {
    var dir = System.IO.Path.Combine(ReleasesDirectory, ReleaseAssets.CurrentRid);
    if (!Directory.Exists(dir)) {
      yield break;
    }
    foreach (var tagDir in Directory.EnumerateDirectories(dir).OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)) {
      var exe = Directory.EnumerateFiles(tagDir, ExecutableName, SearchOption.AllDirectories).FirstOrDefault();
      if (exe != null) {
        yield return (System.IO.Path.GetFileName(tagDir), exe);
      }
    }
  }

  // -----------------------------------------------------------------------------------------------
  // GitHub releases

  private static HttpClient CreateHttpClient() {
    var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("vschromium-bench", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    // Optional: a token raises the rate limit of the GitHub API (60 requests per hour without).
    var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (!string.IsNullOrEmpty(token)) {
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
    return client;
  }

  /// <summary>The releases of the repository (most recent first), or one release ("latest" or a tag).</summary>
  public static async Task<List<Release>> GetReleasesAsync(string? tag, CancellationToken cancellation) {
    using var client = CreateHttpClient();
    var url = tag switch {
      null => $"https://api.github.com/repos/{Repository}/releases?per_page=20",
      "latest" => $"https://api.github.com/repos/{Repository}/releases/latest",
      _ => $"https://api.github.com/repos/{Repository}/releases/tags/{Uri.EscapeDataString(tag)}",
    };
    using var response = await client.GetAsync(url, cancellation);
    if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
      throw new ServerNotFoundException(tag == null || tag == "latest" ? $"No release found in {Repository}." : $"Release \"{tag}\" not found in {Repository}.");
    }
    if (!response.IsSuccessStatusCode) {
      throw new ServerNotFoundException($"GitHub API error {(int)response.StatusCode} {response.ReasonPhrase} ({url}). " +
                                        "Set GITHUB_TOKEN if the rate limit is exceeded.");
    }
    var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation));
    var items = node is JsonArray array ? array.OfType<JsonObject>() : node is JsonObject single ? [single] : [];
    return items.Select(ParseRelease).ToList();
  }

  public static Release ParseRelease(JsonObject json) => new(
    Tag: (string?)json["tag_name"] ?? "",
    Name: (string?)json["name"] ?? "",
    Prerelease: (bool?)json["prerelease"] ?? false,
    Assets: (json["assets"] as JsonArray ?? []).OfType<JsonObject>()
      .Select(a => new ReleaseAsset((string?)a["name"] ?? "", (string?)a["browser_download_url"] ?? "", (long?)a["size"] ?? 0))
      .ToList());

  /// <summary>Downloads (or reuses) the server of a release for this platform.</summary>
  public async Task<ServerCandidate> DownloadReleaseAsync(string tag, CancellationToken cancellation) {
    var rid = ReleaseAssets.CurrentRid;
    var release = (await GetReleasesAsync(tag, cancellation)).FirstOrDefault()
                  ?? throw new ServerNotFoundException($"No release \"{tag}\" in {Repository}.");
    var selection = ReleaseAssets.Select(release.Assets, rid)
                    ?? throw new ServerNotFoundException(ReleaseAssets.DescribeMissing(release, rid) + " Use --server <path> (e.g. a local build).");
    var targetDir = System.IO.Path.Combine(ReleasesDirectory, rid, release.Tag);
    var existing = Directory.Exists(targetDir) ? Directory.EnumerateFiles(targetDir, ExecutableName, SearchOption.AllDirectories).FirstOrDefault() : null;
    if (existing != null) {
      log($"Using the cached server of release {release.Tag}: {existing}");
      return new ServerCandidate("release:" + release.Tag, existing, $"release {release.Tag} {existing}");
    }

    using var client = CreateHttpClient();
    Directory.CreateDirectory(ReleasesDirectory);
    var download = System.IO.Path.Combine(ReleasesDirectory, $"{selection.Package.Name}.download-{Environment.ProcessId}");
    try {
      log($"Downloading {selection.Package.Name} ({selection.Package.Size / 1048576.0:n1} MB) from release {release.Tag}...");
      await using (var stream = await client.GetStreamAsync(selection.Package.DownloadUrl, cancellation))
      await using (var file = File.Create(download)) {
        await stream.CopyToAsync(file, cancellation);
      }
      var length = new FileInfo(download).Length;
      if (selection.Package.Size > 0 && length != selection.Package.Size) {
        throw new ServerNotFoundException($"Download of {selection.Package.Name}: {length} bytes, expected {selection.Package.Size}.");
      }
      if (selection.Checksums != null) {
        var checksums = await client.GetStringAsync(selection.Checksums.DownloadUrl, cancellation);
        var expected = ReleaseAssets.FindChecksum(checksums, selection.Package.Name)
                       ?? throw new ServerNotFoundException($"{selection.Checksums.Name} has no checksum of {selection.Package.Name}.");
        string actual;
        await using (var file = File.OpenRead(download)) {
          actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellation));
        }
        if (actual != expected) {
          throw new ServerNotFoundException($"SHA-256 mismatch of {selection.Package.Name}: {actual}, expected {expected}.");
        }
        log($"SHA-256 verified ({selection.Checksums.Name}).");
      } else {
        log("The release has no checksums: size verified only.");
      }
      var temp = targetDir + ".tmp-" + Environment.ProcessId;
      if (Directory.Exists(temp)) {
        Directory.Delete(temp, recursive: true);
      }
      ZipFile.ExtractToDirectory(download, temp);
      var exe = Directory.EnumerateFiles(temp, ExecutableName, SearchOption.AllDirectories).FirstOrDefault();
      if (exe == null) {
        Directory.Delete(temp, recursive: true);
        throw new ServerNotFoundException($"{selection.Package.Name} contains no {ExecutableName}.");
      }
      Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetDir)!);
      Directory.Move(temp, targetDir);
      exe = Directory.EnumerateFiles(targetDir, ExecutableName, SearchOption.AllDirectories).First();
      if (!OperatingSystem.IsWindows()) {
        File.SetUnixFileMode(exe, File.GetUnixFileMode(exe) | UnixFileMode.UserExecute);
      }
      log($"Server of release {release.Tag}: {exe}");
      return new ServerCandidate("release:" + release.Tag, exe, $"release {release.Tag} {exe}");
    } finally {
      File.Delete(download);
    }
  }
}

/// <summary>The server can't be found or downloaded (message for the user).</summary>
public sealed class ServerNotFoundException(string message) : Exception(message);
