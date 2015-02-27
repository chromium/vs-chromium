// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Text;

namespace VsChromium.Features.QuickSearch {
  public static class SelectionHelpers {
    public static string SelectWordOnly(SnapshotSpan snapshotSpan) {
      if (snapshotSpan.IsEmpty) {
        var line = snapshotSpan.Snapshot.GetLineFromPosition(snapshotSpan.Start);

        // Extend "start" backward as long as it points to a valid word character
        var start = snapshotSpan.Start;
        // If character at "start" is not a word character, try moving back one and check again.
        // If still not, start is in the middle of nowhere.
        // This is to handle cases like this:
        //   foo bar  <= caret at space between foo and bar, we should select "foo"
        //   foo : test  <= caret at ":", we should not select anything
        if (!IsWordCharacter(start.GetChar())) {
          if (start == line.Start)
            return "";

          start = start - 1;
          if (!IsWordCharacter(start.GetChar()))
            return "";
        }
        var adjustedStart = start;

        while (start > line.Start) {
          var ch = (start - 1).GetChar();
          if (!IsWordCharacter(ch))
            break;
          start = start - 1;
        }

        // Extend "end" forward as long as it points to a valid word character
        var end = adjustedStart;
        while (end < line.End - 1) {
          var ch = (end + 1).GetChar();
          if (!IsWordCharacter(ch))
            break;
          end = end + 1;
        }
        end = end + 1;

        return start.Snapshot.GetText(start, end - start);
      } else {
        // Ensure selection is max. one line.
        var line = snapshotSpan.Snapshot.GetLineFromPosition(snapshotSpan.Start);
        var start = snapshotSpan.Start;
        var end = snapshotSpan.End;
        if (start > end) {
          if (end < line.Start) end = line.Start;
          var temp = start;
          start = end;
          end = temp;
        } else {
          if (end > line.End) end = line.End;
        }
        return snapshotSpan.Snapshot.GetText(start, end - start);
      }
    }

    private static bool IsWordCharacter(char ch) {
      return char.IsLetterOrDigit(ch) ||
             ch == '_';
    }
  }
}