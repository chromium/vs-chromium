using Microsoft.VisualStudio.Text;

namespace VsChromiumPackage.Views {
  public interface ITextDocumentService {
    void OnDocumentOpen(ITextDocument document);
    void OnDocumentClose(ITextDocument document);
  }
}