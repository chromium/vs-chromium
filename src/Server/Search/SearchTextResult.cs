using System.Collections.Generic;

namespace VsChromium.Server.Search {
  public class SearchTextResult {
    public static SearchTextResult Empty {
      get {
        return new SearchTextResult { Entries = new List<FileSearchResult>() };
      }
    }

    public IList<FileSearchResult> Entries { get; set; }
    public long HitCount { get; set; }
    public long SearchedFileCount { get; set; }
    public long TotalFileCount { get; set; }
  }
}