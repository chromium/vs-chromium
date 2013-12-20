namespace VsChromiumPackage.Features.BuildOutputAnalyzer {
  public class BuildOutputSpan {
    public string Text { get; set; }
    public int Index { get; set; }
    public int Length { get; set; }
    public string FileName { get; set; }
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
  }
}