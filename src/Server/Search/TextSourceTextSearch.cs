// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public delegate TextRange? FindEntryFunction(TextRange textRange, ParsedSearchString.Entry entry);
  public delegate TextRange GetLineRangeFunction(long position);

  public class TextSourceTextSearch {
    private readonly GetLineRangeFunction _getLineRange;
    private readonly FindEntryFunction _findEntry;

    public TextSourceTextSearch(GetLineRangeFunction getLineRange, FindEntryFunction findEntry) {
      _getLineRange = getLineRange;
      _findEntry = findEntry;
    }

    public IEnumerable<TextRange> FilterOnOtherEntries(
      ParsedSearchString parsedSearchString,
      IEnumerable<TextRange> matches) {
      var getLineExtentCache = new GetLineExtentCache(_getLineRange);
      return matches
        .Select<TextRange, TextRange?>(match => {
          var lineExtent = getLineExtentCache.GetLineExtent(match.CharacterOffset);
          // We got the line extent, the offset at which we found the MainEntry.
          // Now we need to check that "OtherEntries" are present (in order) and
          // in appropriate intervals.
          var range1 = new TextRange(
            lineExtent.CharacterOffset,
            match.CharacterOffset - lineExtent.CharacterOffset);
          var entriesInterval1 = parsedSearchString.EntriesBeforeMainEntry;
          var foundRange1 = entriesInterval1.Any()
            ? CheckEntriesInRange(range1, entriesInterval1)
            : match;

          var range2 = new TextRange(
            match.CharacterEndOffset,
            lineExtent.CharacterEndOffset-  match.CharacterEndOffset);
          var entriesInterval2 = parsedSearchString.EntriesAfterMainEntry;
          var foundRange2 = entriesInterval2.Any()
            ? CheckEntriesInRange(range2, entriesInterval2)
            : match;

          if (foundRange1.HasValue && foundRange2.HasValue) {
            return new TextRange(
              foundRange1.Value.CharacterOffset,
              foundRange2.Value.CharacterEndOffset - foundRange1.Value.CharacterOffset);
          }
          return null;
        })
        .Where(x => x != null)
        .Select(x => x.Value)
        .ToList();
    }

    private TextRange? CheckEntriesInRange(
      TextRange textRange,
      IEnumerable<ParsedSearchString.Entry> entries) {
      TextRange? result = null;
      foreach (var entry in entries) {
        var entryRange = _findEntry(textRange, entry);
        if (entryRange == null) {
          return null;
        }

        long newOffset = entryRange.Value.CharacterEndOffset;
        textRange = new TextRange(newOffset, textRange.CharacterEndOffset - newOffset);

        if (result == null) {
          result = entryRange;
        } else {
          result = new TextRange(result.Value.CharacterOffset, newOffset - result.Value.CharacterOffset);
        }
      }
      return result;
    }
  }
}