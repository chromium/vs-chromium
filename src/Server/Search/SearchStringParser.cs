// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace VsChromium.Server.Search {
  [Export(typeof(ISearchStringParser))]
  public class SearchStringParser : ISearchStringParser {

    public ParsedSearchString Parse(string searchString, SearchStringParserOptions options) {
      SubStrings subStrings;
      switch (options) {
        case SearchStringParserOptions.NoSpecialCharacter:
          subStrings = ParseNoSpecialCharacters(searchString);
          break;
        case SearchStringParserOptions.SupportsAsterisk:
          subStrings = ParseSupportsAsterisk(searchString);
          break;
        case SearchStringParserOptions.SupportsAsteriskAndEscaping:
          subStrings = ParseSupportsAsteriskAndEscaping(searchString);
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(options), options, null);
      }

      // Look for the longest sub string as the main search string.
      var mainIndex = subStrings.List.IndexOf(subStrings.List.OrderByDescending(x => x.Length).FirstOrDefault());
      var longestEntry = subStrings.List.Any() ?
        new ParsedSearchString.Entry { Text = subStrings.List[mainIndex], Index = mainIndex } :
        new ParsedSearchString.Entry { Text = "", Index = -1 };
      var otherEntries = Enumerable
        .Range(0, subStrings.List.Count)
        .Where(i => i != mainIndex)
        .Select(i => new ParsedSearchString.Entry { Text = subStrings.List[i], Index = i })
        .ToList();
      var beforeEntries = otherEntries.Where(e => e.Index < longestEntry.Index);
      var afterEntries = otherEntries.Where(e => e.Index > longestEntry.Index);
      return new ParsedSearchString(longestEntry, beforeEntries, afterEntries);
    }

    private SubStrings ParseNoSpecialCharacters(string searchString) {
      var subStrings = new SubStrings();
      foreach (var c in searchString) {
        subStrings.AddCharacter(c);
      }
      subStrings.FinishCurrent();
      return subStrings;
    }

    private SubStrings ParseSupportsAsterisk(string searchString) {
      var subStrings = new SubStrings();
      foreach (var c in searchString) {
        switch (c) {
          case '*':
            subStrings.FinishCurrent();
            break;
          default:
            subStrings.AddCharacter(c);
            break;
        }
      }
      subStrings.FinishCurrent();
      return subStrings;
    }

    private SubStrings ParseSupportsAsteriskAndEscaping(string searchString) {
      var subStrings = new SubStrings();
      for (var index = 0; index < searchString.Length; index++) {
        var c = searchString[index];
        switch (c) {
          case '*':
            if (index < searchString.Length - 1 && searchString[index + 1] == '*') {
              subStrings.AddCharacter(c);
              index++;
            }
            else {
              subStrings.FinishCurrent();
            }
            break;
          default:
            subStrings.AddCharacter(c);
            break;
        }
      }
      subStrings.FinishCurrent();
      return subStrings;
    }

    private class SubStrings {
      private readonly StringBuilder _current = new StringBuilder();

      public List<string> List { get; } = new List<string>();

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
