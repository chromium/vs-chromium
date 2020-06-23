// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Server.Search {
  public class ParsedSearchString {
    private readonly Entry _mainEntry;
    private readonly IList<Entry> _entriesBeforeMainEntry;
    private readonly IList<Entry> _entriesAfterMainEntry;

    public class Entry {
      public string Text { get; set; }
      public int Index { get; set; }
    }

    public ParsedSearchString(Entry mainEntry, IEnumerable<Entry> entriesBeforeMainEntry, IEnumerable<Entry> entriesAfterMainEntry) {
      _mainEntry = mainEntry;
      _entriesBeforeMainEntry = entriesBeforeMainEntry.OrderBy(x => x.Index).ToList();
      _entriesAfterMainEntry = entriesAfterMainEntry.OrderBy(x => x.Index).ToList();
    }

    public Entry MainEntry {
      get { return _mainEntry; }
    }

    public IList<Entry> EntriesBeforeMainEntry {
      get { return _entriesBeforeMainEntry; }
    }

    public IList<Entry> EntriesAfterMainEntry {
      get { return _entriesAfterMainEntry; }
    }
  }
}