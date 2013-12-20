namespace VsChromiumPackage.Features.BuildOutputAnalyzer {
  public interface IBuildOutputParser {
    BuildOutputSpan ParseLine(string text);
  }
}