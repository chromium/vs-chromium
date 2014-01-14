using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Text;
using VsChromiumCore.FileNames;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumPackage.Threads;

namespace VsChromiumPackage.Views {
  [Export(typeof(ITextDocumentService))]
  public class TextDocumentService : ITextDocumentService {
    private readonly IUIRequestProcessor _uiRequestProcessor;

    [ImportingConstructor]
    public TextDocumentService(IUIRequestProcessor uiRequestProcessor) {
      _uiRequestProcessor = uiRequestProcessor;
    }

    public void OnDocumentOpen(ITextDocument document) {
      var path = document.FilePath;

      if (!IsPhysicalFile(path))
        return;

      var request = new UIRequest {
        Id = "AddFileNameRequest-" + path,
        TypedRequest = new AddFileNameRequest {
          FileName = path
        }
      };

      _uiRequestProcessor.Post(request);
    }

    public void OnDocumentClose(ITextDocument document) {
      var path = document.FilePath;

      if (!IsPhysicalFile(path))
        return;

      var request = new UIRequest {
        Id = "RemoveFileNameRequest-" + path,
        TypedRequest = new RemoveFileNameRequest {
          FileName = path
        }
      };

      _uiRequestProcessor.Post(request);
    }

    private static bool IsPhysicalFile(string path) {
      // This can happen with "Find in files" for example, as it uses a fake filename.
      if (!PathHelpers.IsAbsolutePath(path))
        return false;

      return File.Exists(path);
    }
  }
}