using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VsChromiumPackage.Features.BuildErrors {
  [Export(typeof(IBuildOutputParser))]
  public class BuildOutputParser : IBuildOutputParser {
    private readonly Lazy<Regex> _regex = new Lazy<Regex>(CreateRegex);

    private static Regex CreateRegex() {
      var filenameChars = @"(?:[\\/][\w.][\w.\s]*[\w.]){1,}";
      var dosFilename = @"[a-zA-Z]\:" + filenameChars;
      var netFilename = @"[\\/]" + filenameChars;
      var lineNumberOnly = @"\((?'line'[0-9]+)\)";
      var lineColumn = @"\((?'line'[0-9]+)\s*,\s*(?'col'[0-9]+)\)";
      var regex = string.Format(@"(?'filename'^({0}|{1}))(\s*{2}|\s*{3})?", dosFilename, netFilename, lineNumberOnly, lineColumn);
      return new Regex(regex);
    }

    public BuildOutputSpan ParseLine(string text) {
      var match = _regex.Value.Match(text);
      if (!match.Success)
        return null;

      var filename = match.Groups["filename"].Value;
      Debug.Assert(!string.IsNullOrEmpty(filename));

      int line;
      int.TryParse(match.Groups["line"].Value, out line);
      line--;

      int column;
      int.TryParse(match.Groups["col"].Value, out column);
      column--;
      
      return new BuildOutputSpan {
        FileName = filename,
        LineNumber = line,
        ColumnNumber = column
      };
    }
  }
}