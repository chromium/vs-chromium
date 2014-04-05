using System.Collections.Generic;

namespace VsChromium.Server.Search {
  public class SearchFileContentsResult {
    public static SearchFileContentsResult Empty {
      get {
        return new SearchFileContentsResult { Entries = new List<FileSearchResult>() };
      }
    }

    public IList<FileSearchResult> Entries { get; set; }
    public long HitCount { get; set; }
    public long SearchedFileCount { get; set; }
    public long TotalFileCount { get; set; }
  }
}