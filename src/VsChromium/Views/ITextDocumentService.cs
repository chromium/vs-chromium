using Microsoft.VisualStudio.Text;

namespace VsChromium.Views {
  public interface ITextDocumentService {
    void OnDocumentOpen(ITextDocument document);
    void OnDocumentClose(ITextDocument document);
  }
}