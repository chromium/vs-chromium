namespace VsChromium.Server.Search {
  public interface ISearchStringParser {
    ParsedSearchString Parse(string searchString);
  }
}