namespace VsChromium.Core.Collections {
  public interface IBitArray {
    int Count { get; }
    void Set(int index, bool value);
    bool Get(int index);
  }
}