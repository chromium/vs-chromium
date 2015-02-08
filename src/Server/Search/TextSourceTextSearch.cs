// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Looks for the extent of <paramref name="entry"/> inside the given line
  /// extent.
  /// </summary>
  public delegate TextRange? FindEntryFunction(TextRange textRange, ParsedSearchString.Entry entry);
  /// <summary>
  /// Returns the extent of a line given any position inside the line.
  /// </summary>
  public delegate TextRange GetLineRangeFunction(int position);

  /// <summary>
  /// Implements a delegate that looks for all search entries once the main
  /// search entry has been found.
  /// </summary>
  public class TextSourceTextSearch {
    private readonly FindEntryFunction _findEntry;
    private readonly ParsedSearchString _parsedSearchString;
    private readonly GetLineExtentCache _getLineExtentCache;
    /// <summary>
    /// Keeps track of the previous match so that new matches won't overlap with
    /// previous ones.
    /// </summary>
    private TextRange? _previousMatch;

    public TextSourceTextSearch(
      GetLineRangeFunction getLineRange,
      FindEntryFunction findEntry,
      ParsedSearchString parsedSearchString) {
      _findEntry = findEntry;
      _parsedSearchString = parsedSearchString;
      _getLineExtentCache = new GetLineExtentCache(getLineRange);
    }

    public TextRange? FilterSearchHit(TextRange match) {
      var lineExtent = _getLineExtentCache.GetLineExtent(match.Position);
      if (_previousMatch.HasValue) {
        // If match overlaps with previous one, fail
        if (match.Position < _previousMatch.Value.EndPosition) {
          return null;
        }
        // If line extent overlaps with previous match, shrink it.
        if (lineExtent.Position < _previousMatch.Value.EndPosition) {
          lineExtent = new TextRange(
            _previousMatch.Value.EndPosition,
            lineExtent.EndPosition - _previousMatch.Value.EndPosition);
        }
      }
      // We got the line extent, the offset at which we found the MainEntry.
      // Now we need to check that "OtherEntries" are present (in order) and
      // in appropriate intervals.
      var range1 = new TextRange(
        lineExtent.Position,
        match.Position - lineExtent.Position);
      var entriesInterval1 = _parsedSearchString.EntriesBeforeMainEntry;
      var foundRange1 = entriesInterval1.Any()
        ? CheckEntriesInRange(range1, entriesInterval1)
        : match;

      var range2 = new TextRange(
        match.EndPosition,
        lineExtent.EndPosition - match.EndPosition);
      var entriesInterval2 = _parsedSearchString.EntriesAfterMainEntry;
      var foundRange2 = entriesInterval2.Any()
        ? CheckEntriesInRange(range2, entriesInterval2)
        : match;

      if (foundRange1.HasValue && foundRange2.HasValue) {
        var newMatch = new TextRange(
          foundRange1.Value.Position,
          foundRange2.Value.EndPosition - foundRange1.Value.Position);
        // Save the this match for next iteration
        _previousMatch = newMatch;
        return newMatch;
      }
      return null;
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

        var newOffset = entryRange.Value.EndPosition;
        textRange = new TextRange(newOffset, textRange.EndPosition - newOffset);

        if (result == null) {
          result = entryRange;
        } else {
          result = new TextRange(result.Value.Position, newOffset - result.Value.Position);
        }
      }
      return result;
    }
  }
}