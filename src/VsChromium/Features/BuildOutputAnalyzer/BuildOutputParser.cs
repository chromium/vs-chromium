// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VsChromium.Features.BuildOutputAnalyzer {
  [Export(typeof(IBuildOutputParser))]
  public class BuildOutputParser : IBuildOutputParser {
    private readonly Lazy<Regex> _fullPathRegex = new Lazy<Regex>(CreateFullPathRegex);
    private readonly Lazy<Regex> _filePositionRegex = new Lazy<Regex>(CreateFilePositionRegex);

    private static Regex CreateFullPathRegex() {
      const string directoryNameChars = @"(?:[\\/][\w.][\w.\s]*[\w.]){0,}";
      const string fileNameChars = @"(?:[\\/][\w.]+){1,1}";
      const string dosFullPath = @"[a-zA-Z]\:" + directoryNameChars + fileNameChars;
      const string netFullPath = @"[\\/]" + directoryNameChars + fileNameChars;
      const string lineNumberOnly = @"\((?'line'[0-9]+)\)";
      const string lineColumn = @"\((?'line'[0-9]+)\s*,\s*(?'col'[0-9]+)\)";
      var regex = string.Format(@"^\s*(?'filename'({0}|{1}))(\s*{2}|\s*{3})?", dosFullPath, netFullPath, lineNumberOnly, lineColumn);
      return new Regex(regex);
    }

    private static Regex CreateFilePositionRegex() {
      const string lineNumberOnly = @"\((?'line'[0-9]+)\)";
      const string lineColumn = @"\((?'line'[0-9]+)\s*,\s*(?'col'[0-9]+)\)";
      var regex = string.Format(@"(\s*{0}|\s*{1})?", lineNumberOnly, lineColumn);
      return new Regex(regex);
    }

    public BuildOutputSpan ParseFullPath(string text) {
      var match = _fullPathRegex.Value.Match(text);
      if (!match.Success)
        return null;

      var filenameMatch = match.Groups["filename"];
      var filename = filenameMatch.Value;
      Debug.Assert(!string.IsNullOrEmpty(filename), "RegEx is malformed: it should not match an empty filename");

      int line;
      int column;
      ParseLineColumn(match, out line, out column);

      return new BuildOutputSpan {
        Text = text,
        Index = filenameMatch.Index,
        Length = match.Length - filenameMatch.Index,
        FileName = filename,
        LineNumber = line,
        ColumnNumber = column
      };
    }

    public BuildOutputSpan ParseFullOrRelativePath(string text) {
      text = RemoveColonSuffix(text);
      var split = SplitPathAndPosition(text);
      var path = split.Item1;
      var lineColumn = ParseLineColumn(split.Item2);
      if (lineColumn == null) {
        return new BuildOutputSpan {
          Text = text,
          Index = 0,
          Length = text.Length,
          FileName = text,
          LineNumber = -1,
          ColumnNumber = -1
        };
      }
      return new BuildOutputSpan {
        Text = text,
        Index = 0,
        Length = text.Length,
        FileName = path,
        LineNumber = lineColumn.Item1,
        ColumnNumber = lineColumn.Item2
      };
    }

    private static bool ParseLineColumn(Match match, out int line, out int column) {
      line = column = -1;

      // Line number is mandatory, column is optional.
      var lineValue = match.Groups["line"].Value;
      var columnValue = match.Groups["col"].Value;
      if (string.IsNullOrEmpty(lineValue))
        return false;
      if (string.IsNullOrEmpty(columnValue))
        columnValue = 0.ToString();

      int temp1;
      if (!int.TryParse(lineValue, out temp1))
        return false;

      int temp2;
      if (!int.TryParse(columnValue, out temp2))
        return false;

      line = temp1 - 1;
      column = temp2 - 1;
      return true;
    }

    private Tuple<int, int> ParseLineColumn(string text) {
      var match = _filePositionRegex.Value.Match(text);
      if (!match.Success)
        return null;

      int line;
      int column;
      if (!ParseLineColumn(match, out line, out column))
        return null;
      return Tuple.Create(line, column);
    }

    private Tuple<string, string> SplitPathAndPosition(string text) {
      var indexStart = text.LastIndexOf('(');
      var indexEnd = text.LastIndexOf(')');
      if (indexStart > 0 && indexEnd > indexStart) {
        return Tuple.Create(text.Substring(0, indexStart), text.Substring(indexStart, indexEnd + 1 - indexStart));
      }

      return Tuple.Create(text, "");
    }

    private string RemoveColonSuffix(string text) {
      while (!string.IsNullOrEmpty(text)) {
        var index = text.LastIndexOf(':');
        if (index <= "x:".Length)
          return text;
        text = text.Substring(0, index - 1);
        text = text.Trim();
      }
      return text;
    }
  }
}