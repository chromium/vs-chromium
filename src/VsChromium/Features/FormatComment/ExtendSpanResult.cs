using Microsoft.VisualStudio.Text;

namespace VsChromium.Features.FormatComment {
  public class ExtendSpanResult {
    public CommentType CommentType { get; set; }
    public ITextSnapshotLine StartLine { get; set; }
    public ITextSnapshotLine EndLine { get; set; }
  }
}