// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Server.Search {
  /// <summary>
  /// The result of <see cref="ISearchStringParser.Parse"/>.
  /// </summary>
  public class ParsedSearchString {
    public ParsedSearchString(Entry longestEntry, IEnumerable<Entry> entriesBeforeLongestEntry, IEnumerable<Entry> entriesAfterLongestEntry) {
      LongestEntry = longestEntry;
      EntriesBeforeLongestEntry = entriesBeforeLongestEntry.OrderBy(x => x.Index).ToList();
      EntriesAfterLongestEntry = entriesAfterLongestEntry.OrderBy(x => x.Index).ToList();
    }

    /// <summary>
    /// The longest, hence first, sub-string to search for when performing a search
    /// </summary>
    public Entry LongestEntry { get; }
    /// <summary>
    /// List of sub-strings to match before the <see cref="LongestEntry"/>
    /// </summary>
    public IList<Entry> EntriesBeforeLongestEntry { get; }
    /// <summary>
    /// List of sub-strings to match after the <see cref="LongestEntry"/>
    /// </summary>
    public IList<Entry> EntriesAfterLongestEntry { get; }

    public class Entry {
      /// <summary>
      /// The text that was part of the original search string
      /// </summary>
      public string Text { get; set; }
      /// <summary>
      /// The sequence number of this entry wrt to the initial search string
      /// </summary>
      public int Index { get; set; }
    }
  }
}