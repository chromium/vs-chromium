using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Search {
  public class SearchFileNamesResult {
    public static SearchFileNamesResult Empty { get { return new SearchFileNamesResult { FileNames = new List<FileName>() }; } }

    public IList<FileName> FileNames { get; set; }
    public long TotalCount { get; set; }
  }
}