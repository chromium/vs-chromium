using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Search {
  public class SearchDirectoryNamesResult {
    public static SearchDirectoryNamesResult Empty {
      get {
        return new SearchDirectoryNamesResult {DirectoryNames = new List<DirectoryName>()};
      }
    }

    public IList<DirectoryName> DirectoryNames { get; set; }
    public long TotalCount { get; set; }
  }
}