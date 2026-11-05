// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VsChromium.Core;
using VsChromium.Core.Utility;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Files.PatternMatching;
using VsChromium.Protocol.Generated;
using VsChromium.Server.JsonRpc;

namespace VsChromium.Server.Projects {
  /// <summary>
  /// Builds project-relative ignore patterns and adds patterns to <c>vs-chromium-project.txt</c>
  /// (or the legacy <c>project.vs-chromium-project</c>) while preserving the file's encoding,
  /// byte-order mark, comments, blank lines, and line endings.
  /// </summary>
  public static class ProjectFileEditor {
    /// <summary>
    /// Returns true if <paramref name="project"/> is backed by an editable project file at the
    /// project root (false for Chromium enlistments discovered without a project file).
    /// </summary>
    public static bool TryGetEditableProjectFile(
      IProject project,
      out FullPath projectFilePath,
      out string sourceExplorerIgnoreSectionName) {
      var containingFile = project?.IgnorePathsConfiguration?.ContainingFilePath ?? default;
      if (containingFile != default &&
          containingFile.Parent == project.RootPath &&
          (SystemPathComparer.EqualsNames(containingFile.FileName, ConfigurationFileNames.ProjectFileName) ||
           SystemPathComparer.EqualsNames(containingFile.FileName, ConfigurationFileNames.ProjectFileNameObsolete))) {
        projectFilePath = containingFile;
        sourceExplorerIgnoreSectionName = string.IsNullOrEmpty(project.IgnorePathsConfiguration.Name)
          ? ConfigurationSectionNames.SourceExplorerIgnore
          : project.IgnorePathsConfiguration.Name;
        return true;
      }

      projectFilePath = default;
      sourceExplorerIgnoreSectionName = null;
      return false;
    }

    public static string GetSectionName(IProject project, IgnoreSection section, string sourceExplorerIgnoreSectionName) {
      return section switch {
        IgnoreSection.SourceExplorerIgnore => sourceExplorerIgnoreSectionName ?? ConfigurationSectionNames.SourceExplorerIgnore,
        IgnoreSection.SearchableFilesIgnore => ConfigurationSectionNames.SearchableFilesIgnore,
        _ => throw new RecoverableErrorException($"Unsupported ignore section \"{section}\""),
      };
    }

    /// <summary>
    /// Builds the project-file pattern matching <paramref name="entryName"/> inside
    /// <paramref name="parentRelativePath"/> anchored at the project root (for example
    /// <c>/out/Debug/</c> for a directory or <c>/data/big.bin</c> for a file). Returns
    /// <c>null</c> if the path contains <c>*</c> (which is a wildcard in patterns and cannot be
    /// escaped) or is empty.
    /// </summary>
    public static string BuildExactPattern(string parentRelativePath, string entryName, bool isDirectory) {
      if (string.IsNullOrEmpty(entryName) || entryName.IndexOf('*') >= 0) {
        return null;
      }

      var sb = new StringBuilder();
      sb.Append('/');
      if (!string.IsNullOrEmpty(parentRelativePath)) {
        foreach (var part in PathHelpers.SplitPath(parentRelativePath)) {
          if (string.IsNullOrEmpty(part) || part.IndexOf('*') >= 0) {
            return null;
          }
          sb.Append(part);
          sb.Append('/');
        }
      }

      sb.Append(entryName);
      if (isDirectory) {
        sb.Append('/');
      }
      return sb.ToString();
    }

    /// <summary>
    /// Validates <paramref name="pattern"/> for insertion into a project configuration section and
    /// returns the normalized line to write. Throws <see cref="RecoverableErrorException"/> if the
    /// pattern is empty, a comment, a section header, multi-line, or rejected by
    /// <see cref="PatternParser"/>.
    /// </summary>
    public static string ValidatePattern(string pattern) {
      if (string.IsNullOrWhiteSpace(pattern)) {
        throw new RecoverableErrorException("Ignore pattern must not be empty.");
      }

      var trimmed = pattern.Trim();
      if (trimmed.IndexOf('\r') >= 0 || trimmed.IndexOf('\n') >= 0) {
        throw new RecoverableErrorException("Ignore pattern must be a single line.");
      }
      if (trimmed.StartsWith("#", StringComparison.Ordinal)) {
        throw new RecoverableErrorException("Ignore pattern must not start with '#'.");
      }
      if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)) {
        throw new RecoverableErrorException("Ignore pattern must not be a section header.");
      }

      var parserInput = trimmed
        .Replace('/', Path.DirectorySeparatorChar)
        .Replace('\\', Path.DirectorySeparatorChar);
      PathMatcher matcher;
      try {
        matcher = PatternParser.ParsePattern(parserInput);
      }
      catch (Exception e) {
        throw new RecoverableErrorException($"Invalid ignore pattern \"{trimmed}\": {e.Message}");
      }

      if (matcher.Operators.Count == 1 && matcher.Operators[0] is OpIsNoMatch) {
        throw new RecoverableErrorException($"Invalid ignore pattern \"{trimmed}\".");
      }

      return trimmed;
    }

    /// <summary>
    /// Adds <paramref name="pattern"/> to <paramref name="sectionName"/> in the file at
    /// <paramref name="projectFilePath"/> if not already present.
    /// </summary>
    public static AddIgnorePatternResult AddPatternToFile(
      FullPath projectFilePath,
      string sectionName,
      string pattern) {
      var normalizedLine = ValidatePattern(pattern);
      if (!File.Exists(projectFilePath.Value)) {
        throw new RecoverableErrorException($"Project file \"{projectFilePath.Value}\" does not exist.");
      }

      byte[] existingBytes;
      try {
        existingBytes = File.ReadAllBytes(projectFilePath.Value);
      }
      catch (Exception e) {
        throw new RecoverableErrorException($"Failed to read \"{projectFilePath.Value}\": {e.Message}");
      }

      var updatedBytes = EditProjectFileBytes(existingBytes, sectionName, normalizedLine, out var added, out var resultLine);
      if (added) {
        try {
          File.WriteAllBytes(projectFilePath.Value, updatedBytes);
        }
        catch (Exception e) {
          throw new RecoverableErrorException($"Failed to write \"{projectFilePath.Value}\": {e.Message}");
        }
      }

      return new AddIgnorePatternResult {
        Added = added,
        ProjectFile = projectFilePath.Value,
        SectionName = sectionName,
        Line = resultLine,
      };
    }

    /// <summary>
    /// Pure byte-to-byte transformation of a project file: preserves BOM/encoding, line endings,
    /// comments, and unrelated sections.
    /// </summary>
    public static byte[] EditProjectFileBytes(
      ReadOnlySpan<byte> existingBytes,
      string sectionName,
      string pattern,
      out bool added,
      out string resultLine) {
      var normalizedLine = ValidatePattern(pattern);
      DetectEncoding(existingBytes, out var encoding, out var preambleLength);

      var bodyBytes = existingBytes.Slice(preambleLength).ToArray();
      var text = encoding.GetString(bodyBytes);
      var eol = DetectLineEnding(text);
      var lines = SplitLines(text);

      var currentSection = "";
      var sectionHeaderIndex = -1;
      var lastNonBlankIndexInSection = -1;

      for (var i = 0; i < lines.Count; i++) {
        var lineSpan = lines[i];
        var lineText = text.Substring(lineSpan.Start, lineSpan.ContentEnd - lineSpan.Start);
        var parsedSection = ParseSectionName(lineText);
        if (parsedSection != null) {
          currentSection = parsedSection;
          if (string.Equals(currentSection, sectionName, StringComparison.Ordinal)) {
            sectionHeaderIndex = i;
          }
          continue;
        }

        if (!string.Equals(currentSection, sectionName, StringComparison.Ordinal)) {
          continue;
        }

        if (!string.IsNullOrWhiteSpace(lineText)) {
          lastNonBlankIndexInSection = i;
          if (!IsCommentLine(lineText) && PatternsEqual(lineText, normalizedLine)) {
            added = false;
            resultLine = lineText;
            return existingBytes.ToArray();
          }
        }
      }

      string updatedText;
      if (sectionHeaderIndex >= 0) {
        var anchorIndex = lastNonBlankIndexInSection >= 0 ? lastNonBlankIndexInSection : sectionHeaderIndex;
        var anchor = lines[anchorIndex];
        var anchorHasEol = anchor.NextStart > anchor.ContentEnd;
        var insertion = anchorHasEol
          ? normalizedLine + eol
          : eol + normalizedLine + eol;
        updatedText = text.Insert(anchor.NextStart, insertion);
      } else {
        var prefix = GetNewSectionPrefix(text, eol);
        updatedText = text + prefix + "[" + sectionName + "]" + eol + normalizedLine + eol;
      }

      added = true;
      resultLine = normalizedLine;

      var preamble = encoding.GetPreamble();
      var encodedBody = encoding.GetBytes(updatedText);
      if (preambleLength == 0 || preamble.Length == 0) {
        // Note: When preambleLength > 0 we chose an encoding whose preamble matches the original
        // BOM bytes; copy the exact original BOM bytes just in case.
        if (preambleLength == 0) {
          return encodedBody;
        }
      }

      var output = new byte[preambleLength + encodedBody.Length];
      existingBytes.Slice(0, preambleLength).CopyTo(output);
      Buffer.BlockCopy(encodedBody, 0, output, preambleLength, encodedBody.Length);
      return output;
    }

    private static void DetectEncoding(ReadOnlySpan<byte> bytes, out Encoding encoding, out int preambleLength) {
      if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
        encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        preambleLength = 3;
        return;
      }
      if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
        encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        preambleLength = 2;
        return;
      }
      if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) {
        encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        preambleLength = 2;
        return;
      }

      preambleLength = 0;
      var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
      try {
        _ = strictUtf8.GetCharCount(bytes);
        encoding = strictUtf8;
      }
      catch (DecoderFallbackException) {
        encoding = Encoding.Latin1;
      }
    }

    private static string DetectLineEnding(string text) {
      var crlfCount = 0;
      var bareLfCount = 0;
      for (var i = 0; i < text.Length; i++) {
        if (text[i] == '\n') {
          if (i > 0 && text[i - 1] == '\r') {
            crlfCount++;
          } else {
            bareLfCount++;
          }
        }
      }

      if (crlfCount == 0 && bareLfCount == 0) {
        return OperatingSystem.IsWindows() ? "\r\n" : "\n";
      }
      return crlfCount >= bareLfCount ? "\r\n" : "\n";
    }

    private static string GetNewSectionPrefix(string text, string eol) {
      if (text.Length == 0) {
        return "";
      }
      if (EndsWithBlankLine(text)) {
        return "";
      }
      if (text[text.Length - 1] == '\n') {
        return eol;
      }
      return eol + eol;
    }

    private static bool EndsWithBlankLine(string text) {
      var i = text.Length - 1;
      if (i < 0 || text[i] != '\n') {
        return false;
      }
      i--;
      if (i >= 0 && text[i] == '\r') {
        i--;
      }
      return i >= 0 && text[i] == '\n';
    }

    private static string ParseSectionName(string line) {
      var trimmed = line.Trim();
      if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']') {
        return trimmed.Substring(1, trimmed.Length - 2).Trim();
      }
      return null;
    }

    private static bool IsCommentLine(string line) {
      return line.TrimStart().StartsWith("#", StringComparison.Ordinal);
    }

    private static bool PatternsEqual(string existingLine, string normalizedPattern) {
      var a = existingLine.Trim().Replace('\\', '/');
      var b = normalizedPattern.Trim().Replace('\\', '/');
      return SystemPathComparer.Instance.StringComparer.Equals(a, b);
    }

    private readonly struct LineSpan {
      public LineSpan(int start, int contentEnd, int nextStart) {
        Start = start;
        ContentEnd = contentEnd;
        NextStart = nextStart;
      }

      public int Start { get; }
      public int ContentEnd { get; }
      public int NextStart { get; }
    }

    private static List<LineSpan> SplitLines(string text) {
      var result = new List<LineSpan>();
      var i = 0;
      while (i < text.Length) {
        var start = i;
        while (i < text.Length && text[i] != '\r' && text[i] != '\n') {
          i++;
        }
        var contentEnd = i;
        if (i < text.Length && text[i] == '\r') {
          i++;
        }
        if (i < text.Length && text[i] == '\n') {
          i++;
        }
        result.Add(new LineSpan(start, contentEnd, i));
      }
      return result;
    }
  }
}
