using System.Collections.Generic;

namespace VsChromium.Server.Search {
  public class SearchCodeResult {
    public static SearchCodeResult Empty {
      get {
        return new SearchCodeResult { Entries = new List<FileSearchResult>() };
      }
    }

    public IList<FileSearchResult> Entries { get; set; }
    public long HitCount { get; set; }
    public long SearchedFileCount { get; set; }
    public long TotalFileCount { get; set; }
  }
}