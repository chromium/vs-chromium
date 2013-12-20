using Microsoft.VisualStudio.Text;

namespace VsChromiumPackage.Features.FormatComment {
  public interface ICommentFormatter {
    ExtendSpanResult ExtendSpan(SnapshotSpan span);
    FormatLinesResult FormatLines(ExtendSpanResult span);
    bool ApplyChanges(ITextEdit textEdit, FormatLinesResult result);
  }
}