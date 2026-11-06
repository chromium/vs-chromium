// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace VsChromium.Bench.Servers;

/// <summary>An asset of a GitHub release.</summary>
public sealed record ReleaseAsset(string Name, string DownloadUrl, long Size);

/// <summary>A GitHub release.</summary>
public sealed record Release(string Tag, string Name, bool Prerelease, IReadOnlyList<ReleaseAsset> Assets);

/// <summary>The assets of a release to use for a platform: the server package, and the checksums file if any.</summary>
public sealed record AssetSelection(ReleaseAsset Package, ReleaseAsset? Checksums, bool IsVsix);

/// <summary>
/// Pure logic of the selection of the server package of a release: assets with clear names only,
/// <c>vschromium-server-&lt;version&gt;-&lt;rid&gt;.zip</c> (preferred) or
/// <c>vs-chromium-&lt;version&gt;-&lt;VS Code target&gt;.vsix</c> (the per-platform VS Code extension
/// package, a zip file), and the SHA-256 checksums.
/// </summary>
public static partial class ReleaseAssets {
  /// <summary>The runtime identifier of this machine: win-x64, win-arm64, linux-x64, linux-arm64, osx-arm64, osx-x64.</summary>
  public static string CurrentRid {
    get {
      var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
      var arch = RuntimeInformation.OSArchitecture switch {
        Architecture.Arm64 => "arm64",
        Architecture.X86 => "x86",
        Architecture.Arm => "arm",
        _ => "x64",
      };
      return $"{os}-{arch}";
    }
  }

  /// <summary>Selects the package of <paramref name="rid"/>, or null if the release has none.</summary>
  public static AssetSelection? Select(IReadOnlyList<ReleaseAsset> assets, string rid) {
    var suffix = "-" + rid;
    ReleaseAsset? Find(string prefix, string extension) => FindWithSuffix(prefix, suffix, extension);
    ReleaseAsset? FindWithSuffix(string prefix, string platformSuffix, string extension) => assets
      .Where(a => a.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                  a.Name.EndsWith(platformSuffix + extension, StringComparison.OrdinalIgnoreCase) &&
                  a.Name.Length > prefix.Length + platformSuffix.Length + extension.Length)
      .OrderByDescending(a => a.Name, StringComparer.OrdinalIgnoreCase)
      .FirstOrDefault();
    // The VS Code extension packages of the releases are named after the VS Code target platform
    // (vs-chromium-<version>-win32-x64.vsix, see docs/release.md); runtime identifier names
    // (vs-chromium-<version>-win-x64.vsix) are accepted too.
    ReleaseAsset? FindVsix() =>
      (VsCodeTarget(rid) is { } target ? FindWithSuffix("vs-chromium-", "-" + target, ".vsix") : null) ?? Find("vs-chromium-", ".vsix");
    var zip = Find("vschromium-server-", ".zip");
    var vsix = zip == null ? FindVsix() : null;
    var package = zip ?? vsix;
    if (package == null) {
      return null;
    }
    var checksums = assets.FirstOrDefault(a => a.Name.Equals(package.Name + ".sha256", StringComparison.OrdinalIgnoreCase))
                    ?? assets.FirstOrDefault(a => a.Name.ToUpperInvariant() is "SHA256SUMS" or "SHA256SUMS.TXT" or "CHECKSUMS.TXT" or "SHA256SUMS.SHA256");
    return new AssetSelection(package, checksums, IsVsix: vsix != null);
  }

  /// <summary>
  /// The VS Code target platform of a runtime identifier (win-x64 -> win32-x64, osx-arm64 ->
  /// darwin-arm64, linux-x64 -> linux-x64), or null for an unknown runtime identifier.
  /// </summary>
  public static string? VsCodeTarget(string rid) => rid switch {
    "win-x64" => "win32-x64",
    "win-arm64" => "win32-arm64",
    "linux-x64" => "linux-x64",
    "linux-arm64" => "linux-arm64",
    "osx-x64" => "darwin-x64",
    "osx-arm64" => "darwin-arm64",
    _ => null,
  };

  /// <summary>
  /// The SHA-256 of <paramref name="fileName"/> in a checksums file: "&lt;hex&gt;  &lt;name&gt;" or
  /// "&lt;hex&gt; *&lt;name&gt;" lines (sha256sum format), or a single hash (a "&lt;asset&gt;.sha256" file).
  /// </summary>
  public static string? FindChecksum(string checksumsText, string fileName) {
    var lines = checksumsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var line in lines) {
      var match = ChecksumLine().Match(line);
      if (match.Success && string.Equals(Path.GetFileName(match.Groups["name"].Value), fileName, StringComparison.OrdinalIgnoreCase)) {
        return match.Groups["hash"].Value.ToLowerInvariant();
      }
    }
    if (lines.Length == 1 && HashOnly().Match(lines[0]) is { Success: true } single) {
      return single.Groups["hash"].Value.ToLowerInvariant();
    }
    return null;
  }

  /// <summary>Description of a release without a package for this platform (for the user).</summary>
  public static string DescribeMissing(Release release, string rid) =>
    $"Release {release.Tag} has no server for {rid} (looked for vschromium-server-<version>-{rid}.zip or " +
    $"vs-chromium-<version>-{rid}.vsix; assets: {(release.Assets.Count == 0 ? "none" : string.Join(", ", release.Assets.Select(a => a.Name)))}).";

  [GeneratedRegex(@"^(?<hash>[0-9a-fA-F]{64})\s+\*?(?<name>\S.*)$")]
  private static partial Regex ChecksumLine();

  [GeneratedRegex(@"^(?<hash>[0-9a-fA-F]{64})(\s.*)?$")]
  private static partial Regex HashOnly();
}
