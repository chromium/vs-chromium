namespace VsChromiumPackage.Features.BuildErrors {
  public interface IBuildOutputParser {
    BuildOutputSpan ParseLine(string text);
  }
}