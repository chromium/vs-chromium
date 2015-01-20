using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public interface ICompiledTextSearchProviderFactory {
    ICompiledTextSearchProvider CreateSearchAlgorithmProvider(string pattern, NativeMethods.SearchOptions searchOptions);
  }
}