using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace VsChromiumPackage.Features.FormatComment {
  public class FormatLinesResult {
    public CommentType CommentType { get; set; }
    public SnapshotSpan SnapshotSpan { get; set; }
    public int Indent { get; set; }
    public IList<string> Lines { get; set; }
  }
}