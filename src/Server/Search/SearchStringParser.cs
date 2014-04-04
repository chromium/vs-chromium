using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace VsChromium.Server.Search {
  [Export(typeof(ISearchStringParser))]
  public class SearchStringParser : ISearchStringParser {
    public ParsedSearchString Parse(string searchString) {
      var subStrings = new SubStrings();
      bool inEscapeSequence = false;
      foreach (var c in searchString) {
        switch (c) {
          case '\\':
            if (inEscapeSequence) {
              subStrings.AddCharacter('\\');
              inEscapeSequence = false;
            } else {
              inEscapeSequence = true;
            }
            break;
          case '*':
            if (inEscapeSequence) {
              subStrings.AddCharacter('*');
              inEscapeSequence = false;
            } else {
              subStrings.FinishCurrent();
            }
            break;
          default:
            inEscapeSequence = false;
            subStrings.AddCharacter(c);
            break;
        }
      }
      subStrings.FinishCurrent();

      // Look for the longest sub string as the main search string.
      int mainIndex = subStrings.List.IndexOf(subStrings.List.OrderByDescending(x => x.Length).FirstOrDefault());
      var mainEntry = subStrings.List.Any() ?
                        new ParsedSearchString.Entry {Text = subStrings.List[mainIndex], Index = mainIndex} :
                        new ParsedSearchString.Entry {Text = "", Index = -1};
      var otherEntries = Enumerable
        .Range(0, subStrings.List.Count)
        .Where(i => i != mainIndex)
        .Select(i => new ParsedSearchString.Entry { Text = subStrings.List[i], Index = i })
        .ToList();
      return new ParsedSearchString(mainEntry, otherEntries.Where(e => e.Index < mainEntry.Index), otherEntries.Where(e => e.Index > mainEntry.Index));
    }

    class SubStrings {
      private readonly StringBuilder _current = new StringBuilder();
      private readonly List<string> _list = new List<string>();

      public List<string> List { get { return _list; } }

      public void AddCharacter(char c) {
        _current.Append(c);
      }

      public void FinishCurrent() {
        if (_current.Length > 0) {
          List.Add(_current.ToString());
        }
        _current.Clear();
      }
    }
  }
}