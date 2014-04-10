// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Search {
  public class TextSourceTextSearch {
    private readonly Func<int, FilePositionSpan> _getLineExtent;
    private readonly Func<int, char> _getCharacterAt;

    public TextSourceTextSearch(Func<int, FilePositionSpan> getLineExtent, Func<int, char> getCharacterAt) {
      _getLineExtent = getLineExtent;
      _getCharacterAt = getCharacterAt;
    }

    public IEnumerable<FilePositionSpan> FilterOnOtherEntries(ParsedSearchString parsedSearchString, bool matchCase, IEnumerable<FilePositionSpan> matches) {
      var factory = new GetLineExtentFactory(_getLineExtent);
      return matches
        .Select(match => {
          var lineExtent = factory.GetLineExtent(match.Position);
          // We got the line extent, the offset at which we found the MainEntry.
          // Now we need to check that "OtherEntries" are present (in order) and
          // in appropriate intervals.
          var positionInterval1 = lineExtent.Position;
          var lengthInterval1 = match.Position - lineExtent.Position;
          var entriesInterval1 = parsedSearchString.EntriesBeforeMainEntry;
          var positionInterval2 = match.Position + match.Length;
          var lengthInterval2 = (lineExtent.Position + lineExtent.Length) - (match.Position + match.Length);
          var entriesInterval2 = parsedSearchString.EntriesAfterMainEntry;

          int foundPositionInterval1;
          int foundLengthInterval1;
          int foundPositionInterval2;
          int foundLengthInterval2;
          if (CheckEntriesInInterval(positionInterval1, lengthInterval1, entriesInterval1, matchCase, out foundPositionInterval1, out foundLengthInterval1) &&
              CheckEntriesInInterval(positionInterval2, lengthInterval2, entriesInterval2, matchCase, out foundPositionInterval2, out foundLengthInterval2)) {

            // If there was no entries before MainEntry, adjust interval
            // location to be the same as the main entry.
            if (foundPositionInterval1 < 0) {
              foundPositionInterval1 = match.Position;
              foundLengthInterval1 = match.Length;
            }
            // If there was no entries after MainEntry, adjust interval to be
            // the same as the main entry.
            if (foundPositionInterval2 < 0) {
              foundPositionInterval2 = match.Position;
              foundLengthInterval2 = match.Length;
            }
            return new FilePositionSpan {
              Position = foundPositionInterval1,
              Length = foundPositionInterval2 + foundLengthInterval2 - foundPositionInterval1
            };
          }
          return new FilePositionSpan();
        })
        .Where(x => x.Length != default(FilePositionSpan).Length)
        .ToList();
    }

    private bool CheckEntriesInInterval(int position, int length, IEnumerable<ParsedSearchString.Entry> entries, bool matchCase, out int foundPosition, out int foundLength) {
      foundPosition = -1;
      foundLength = 0;
      foreach (var entry in entries) {
        var offset = FindEntry(position, length, entry.Text, matchCase);
        if (offset < 0) {
          foundPosition = -1;
          foundLength = 0;
          return false;
        }

        int advance = offset - position;
        position += advance;
        length -= advance;

        if (foundPosition == -1) {
          foundPosition = offset;
          foundLength = entry.Text.Length;
        } else {
          foundLength = (offset + entry.Text.Length) - foundPosition;
        }
      }
      return true;
    }

    private int FindEntry(int position, int length, string text, bool matchCase) {
      int compareCount = length - text.Length;
      for (var i = 0; i < compareCount; i++) {
        if (MatchEntryText(position + i, text, matchCase))
          return position + i;
      }
      return -1;
    }

    private bool MatchEntryText(int position, string text, bool matchCase) {
      for (var i = 0; i < text.Length; i++) {
        var ch1 = _getCharacterAt(position + i);
        var ch2 = text[i];
        if (!matchCase) {
          ch1 = char.ToLowerInvariant(ch1);
          ch2 = char.ToLowerInvariant(ch2);
        }

        if (ch1 != ch2)
          return false;
      }
      return true;
    }
  }
}