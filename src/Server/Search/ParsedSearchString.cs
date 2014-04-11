using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class ParsedSearchString : IDisposable {
    private readonly Entry _mainEntry;
    private readonly IList<Entry> _entriesBeforeMainEntry;
    private readonly IList<Entry> _entriesAfterMainEntry;

    public class Entry : IDisposable {
      public string Text { get; set; }
      public int Index { get; set; }
      public AsciiStringSearchAlgorithm AsciiStringSearchAlgo { get; set; }
      public UTF16StringSearchAlgorithm UTF16StringSearchAlgo { get; set; }

      public void Dispose() {
        if (AsciiStringSearchAlgo != null)
          AsciiStringSearchAlgo.Dispose();
        if (UTF16StringSearchAlgo != null)
          UTF16StringSearchAlgo.Dispose();
      }
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

    public void Dispose() {
      _mainEntry.Dispose();
      foreach (var entry in _entriesBeforeMainEntry)
        entry.Dispose();
      foreach (var entry in _entriesAfterMainEntry)
        entry.Dispose();
    }
  }
}