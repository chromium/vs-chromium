// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Search {
  public delegate int FindEntry(int position, int length, ParsedSearchString.Entry entry);

  public class TextSourceTextSearch {
    private readonly Func<int, FilePositionSpan> _getLineExtent;
    private readonly FindEntry _findEntry;

    public TextSourceTextSearch(Func<int, FilePositionSpan> getLineExtent, FindEntry findEntry) {
      _getLineExtent = getLineExtent;
      _findEntry = findEntry;
    }

    public IEnumerable<FilePositionSpan> FilterOnOtherEntries(ParsedSearchString parsedSearchString, IEnumerable<FilePositionSpan> matches) {
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
          if (CheckEntriesInInterval(positionInterval1, lengthInterval1, entriesInterval1, out foundPositionInterval1, out foundLengthInterval1) &&
              CheckEntriesInInterval(positionInterval2, lengthInterval2, entriesInterval2, out foundPositionInterval2, out foundLengthInterval2)) {

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

    private bool CheckEntriesInInterval(int position, int length, IEnumerable<ParsedSearchString.Entry> entries, out int foundPosition, out int foundLength) {
      foundPosition = -1;
      foundLength = 0;
      foreach (var entry in entries) {
        var offset = _findEntry(position, length, entry);
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
  }
}