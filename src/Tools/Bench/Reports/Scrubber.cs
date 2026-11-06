// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;
using System.Text.RegularExpressions;

namespace VsChromium.Bench.Reports;

/// <summary>A sensitive value and its replacement in a shareable report.</summary>
public sealed record SensitiveTerm(string Value, string Replacement, bool IsPath);

/// <summary>Values found in a report after scrubbing: the report must not be written.</summary>
public sealed class SensitiveDataException(IReadOnlyList<string> found)
  : Exception("The report still contains identifying data: " + string.Join(", ", found) + ". Not written.") {
  public IReadOnlyList<string> Found { get; } = found;
}

/// <summary>
/// The safety net of the shareable reports: replaces the user name, the machine name, the domain, the
/// user profile directory and the absolute paths of the run (case-insensitive, with any slash style)
/// by placeholders, then checks that none of them is left, nor a SID or a full GUID.
/// </summary>
public static partial class Scrubber {
  /// <summary>Terms shorter than this are not scrubbed (they would match ordinary words).</summary>
  public const int MinimumTermLength = 3;

  /// <summary>
  /// The terms of the current machine: user, machine, domain, user profile, plus the given paths
  /// (corpora, cache, server, bench...).
  /// </summary>
  public static List<SensitiveTerm> CurrentMachineTerms(IEnumerable<(string Path, string Placeholder)> paths) {
    var terms = new List<SensitiveTerm>();
    foreach (var (path, placeholder) in paths) {
      if (!string.IsNullOrWhiteSpace(path)) {
        terms.Add(new SensitiveTerm(path, placeholder, IsPath: true));
      }
    }
    var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    if (!string.IsNullOrEmpty(profile)) {
      terms.Add(new SensitiveTerm(profile, "<user-profile>", IsPath: true));
    }
    terms.Add(new SensitiveTerm(Environment.MachineName, "<machine>", IsPath: false));
    terms.Add(new SensitiveTerm(Environment.UserDomainName, "<domain>", IsPath: false));
    terms.Add(new SensitiveTerm(Environment.UserName, "<user>", IsPath: false));
    return terms;
  }

  /// <summary>Replaces the terms (longest first, so that a path is replaced before the names it contains).</summary>
  public static string Scrub(string text, IEnumerable<SensitiveTerm> terms) {
    foreach (var term in Usable(terms).OrderByDescending(t => t.Value.Length)) {
      text = Pattern(term).Replace(text, term.Replacement);
    }
    return text;
  }

  /// <summary>The terms still present (and SIDs, full GUIDs).</summary>
  public static List<string> Find(string text, IEnumerable<SensitiveTerm> terms) {
    var found = new List<string>();
    foreach (var term in Usable(terms)) {
      if (Pattern(term).IsMatch(text)) {
        found.Add($"{term.Replacement} (\"{term.Value}\")");
      }
    }
    foreach (Match match in SidPattern().Matches(text)) {
      found.Add($"SID \"{match.Value}\"");
    }
    foreach (Match match in GuidPattern().Matches(text)) {
      found.Add($"GUID \"{match.Value}\"");
    }
    return found;
  }

  /// <summary>Scrubs, then throws <see cref="SensitiveDataException"/> if anything is left.</summary>
  public static string ScrubAndVerify(string text, IEnumerable<SensitiveTerm> terms) {
    var list = terms.ToList();
    var scrubbed = Scrub(text, list);
    var found = Find(scrubbed, list);
    if (found.Count > 0) {
      throw new SensitiveDataException(found);
    }
    return scrubbed;
  }

  private static IEnumerable<SensitiveTerm> Usable(IEnumerable<SensitiveTerm> terms) =>
    terms.Where(t => t.Value.Trim().TrimEnd('/', '\\').Length >= MinimumTermLength)
      .Select(t => t with { Value = t.IsPath ? t.Value.Trim().TrimEnd('/', '\\') : t.Value.Trim() })
      .DistinctBy(t => t.Value.ToUpperInvariant());

  /// <summary>
  /// The pattern of a term: case-insensitive; separators of paths match any number of "\" or "/"
  /// (e.g. "C:\Users\me", "c:/users/me", JSON escaped "C:\\Users\\me"); not inside a longer word.
  /// </summary>
  private static Regex Pattern(SensitiveTerm term) {
    var pattern = new StringBuilder("(?<![A-Za-z0-9_])");
    foreach (var c in term.Value) {
      pattern.Append(c is '\\' or '/' ? @"[\\/]+" : Regex.Escape(c.ToString()));
    }
    pattern.Append("(?![A-Za-z0-9_])");
    return new Regex(pattern.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
  }

  [GeneratedRegex(@"\bS-1-\d+(-\d+){2,}\b")]
  private static partial Regex SidPattern();

  [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
  private static partial Regex GuidPattern();
}
