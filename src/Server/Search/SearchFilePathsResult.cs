using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Search {
  public class SearchFilePathsResult {
    public static SearchFilePathsResult Empty { get { return new SearchFilePathsResult { FileNames = new List<FileName>() }; } }

    public IList<FileName> FileNames { get; set; }
    public long TotalCount { get; set; }
  }
}