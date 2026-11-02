// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;

namespace VsChromium.Server.TextSearch {
  public static class RegexLiterals {
    /// <summary>
    /// Returns the longest sequence of literal characters that every match of
    /// the regular expression <paramref name="pattern"/> must contain, or
    /// <code>null</code> if there is none (or if the pattern is too complex for
    /// this simple analysis). The analysis is conservative: it only looks at
    /// the top level sequence of the pattern (no alternation, groups are
    /// skipped), and ignores characters followed by a quantifier. Used to skip
    /// text that can't contain a match without running the regex.
    /// </summary>
    public static string GetRequiredLiteral(string pattern) {
      // Inline options (e.g. "(?i)") and top level alternations change the
      // meaning of the whole pattern: give up.
      if (pattern.Contains("(?"))
        return null;

      string longest = null;
      var current = new StringBuilder();
      var depth = 0;

      void EndRun() {
        if (current.Length > 0 && (longest == null || current.Length > longest.Length))
          longest = current.ToString();
        current.Clear();
      }

      var index = 0;
      while (index < pattern.Length) {
        var ch = pattern[index];
        if (depth > 0) {
          // Skip the contents of groups (but track nesting and escapes).
          if (ch == '\\') {
            index += 2;
            continue;
          }
          if (ch == '[') {
            index = SkipCharacterClass(pattern, index);
            continue;
          }
          if (ch == '(')
            depth++;
          else if (ch == ')')
            depth--;
          index++;
          if (depth == 0)
            SkipQuantifier(pattern, ref index);
          continue;
        }

        switch (ch) {
          case '|':
            // Top level alternation: no required literal.
            return null;
          case '(':
            EndRun();
            depth++;
            index++;
            continue;
          case '[':
            EndRun();
            index = SkipCharacterClass(pattern, index);
            SkipQuantifier(pattern, ref index);
            continue;
          case '.':
          case '^':
          case '$':
            EndRun();
            index++;
            SkipQuantifier(pattern, ref index);
            continue;
          case '*':
          case '+':
          case '?':
          case '{':
          case ')':
            // Unexpected (e.g. quantifier at the beginning): give up.
            return null;
        }

        char? literal;
        int next;
        if (ch == '\\') {
          if (index + 1 >= pattern.Length)
            return null;
          var escaped = pattern[index + 1];
          // Escaped punctuation is a literal; letters and digits are classes
          // (\w, \d...), anchors (\b), back references or special characters
          // (\n, \t, \x41...): give up on the literal for those.
          literal = (escaped < 128 && !char.IsLetterOrDigit(escaped)) ? escaped : (char?)null;
          next = index + 2;
        } else {
          literal = ch;
          next = index + 1;
        }

        if (next < pattern.Length && IsQuantifier(pattern[next])) {
          // The character is optional or repeated: it ends the run.
          EndRun();
          index = next;
          SkipQuantifier(pattern, ref index);
          continue;
        }

        if (literal == null) {
          EndRun();
        } else {
          current.Append(literal.Value);
        }
        index = next;
      }

      if (depth != 0)
        return null;
      EndRun();
      return longest;
    }

    private static bool IsQuantifier(char ch) {
      return ch == '*' || ch == '+' || ch == '?' || ch == '{';
    }

    private static void SkipQuantifier(string pattern, ref int index) {
      if (index >= pattern.Length)
        return;
      var ch = pattern[index];
      if (ch == '*' || ch == '+' || ch == '?') {
        index++;
      } else if (ch == '{') {
        var end = pattern.IndexOf('}', index);
        index = end < 0 ? pattern.Length : end + 1;
      } else {
        return;
      }
      // Lazy quantifier
      if (index < pattern.Length && pattern[index] == '?')
        index++;
    }

    private static int SkipCharacterClass(string pattern, int index) {
      // index is at '['
      index++;
      if (index < pattern.Length && pattern[index] == '^')
        index++;
      // A ']' right after '[' or '[^' is a literal.
      if (index < pattern.Length && pattern[index] == ']')
        index++;
      while (index < pattern.Length) {
        var ch = pattern[index];
        if (ch == '\\') {
          index += 2;
          continue;
        }
        if (ch == ']')
          return index + 1;
        index++;
      }
      return pattern.Length;
    }
  }
}
