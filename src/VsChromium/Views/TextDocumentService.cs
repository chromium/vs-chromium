using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Threads;

namespace VsChromium.Views {
  [Export(typeof(ITextDocumentService))]
  public class TextDocumentService : ITextDocumentService {
    private readonly IUIRequestProcessor _uiRequestProcessor;
    private readonly IFileSystem _fileSystem;

    [ImportingConstructor]
    public TextDocumentService(IUIRequestProcessor uiRequestProcessor, IFileSystem fileSystem) {
      _uiRequestProcessor = uiRequestProcessor;
      _fileSystem = fileSystem;
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

    private bool IsPhysicalFile(string path) {
      // This can happen with "Find in files" for example, as it uses a fake filename.
      if (!PathHelpers.IsAbsolutePath(path))
        return false;

      return _fileSystem.FileExists(new FullPath(path));
    }
  }
}