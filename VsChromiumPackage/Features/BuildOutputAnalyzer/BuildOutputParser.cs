using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VsChromiumPackage.Features.BuildOutputAnalyzer {
  [Export(typeof(IBuildOutputParser))]
  public class BuildOutputParser : IBuildOutputParser {
    private readonly Lazy<Regex> _regex = new Lazy<Regex>(CreateRegex);

    private static Regex CreateRegex() {
      const string directoryNameChars = @"(?:[\\/][\w.][\w.\s]*[\w.]){0,}";
      const string fileNameChars = @"(?:[\\/][\w.]+){1,1}";
      const string dosFullPath = @"[a-zA-Z]\:" + directoryNameChars + fileNameChars;
      const string netFullPath = @"[\\/]" + directoryNameChars + fileNameChars;
      const string lineNumberOnly = @"\((?'line'[0-9]+)\)";
      const string lineColumn = @"\((?'line'[0-9]+)\s*,\s*(?'col'[0-9]+)\)";
      var regex = string.Format(@"^\s*(?'filename'({0}|{1}))(\s*{2}|\s*{3})?", dosFullPath, netFullPath, lineNumberOnly, lineColumn);
      return new Regex(regex);
    }

    public BuildOutputSpan ParseLine(string text) {
      var match = _regex.Value.Match(text);
      if (!match.Success)
        return null;

      var filenameMatch = match.Groups["filename"];
      var filename = filenameMatch.Value;
      Debug.Assert(!string.IsNullOrEmpty(filename));

      int line;
      int.TryParse(match.Groups["line"].Value, out line);
      line--;

      int column;
      int.TryParse(match.Groups["col"].Value, out column);
      column--;
      
      return new BuildOutputSpan {
        Text = text,
        Index = filenameMatch.Index,
        Length = match.Length - filenameMatch.Index,
        FileName = filename,
        LineNumber = line,
        ColumnNumber = column
      };
    }
  }
}