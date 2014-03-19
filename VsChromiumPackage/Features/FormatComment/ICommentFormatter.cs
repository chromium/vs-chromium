using Microsoft.VisualStudio.Text;

namespace VsChromium.Features.FormatComment {
  public interface ICommentFormatter {
    ExtendSpanResult ExtendSpan(SnapshotSpan span);
    FormatLinesResult FormatLines(ExtendSpanResult span);
    bool ApplyChanges(ITextEdit textEdit, FormatLinesResult result);
  }
}