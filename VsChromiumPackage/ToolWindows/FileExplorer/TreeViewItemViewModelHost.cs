using VsChromiumPackage.Threads;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  public class TreeViewItemViewModelHost : ITreeViewItemViewModelHost {
    private readonly IStandarImageSourceFactory _standarImageSourceFactory;
    private readonly IUIRequestProcessor _uiRequestProcessor;

    public TreeViewItemViewModelHost(IStandarImageSourceFactory standarImageSourceFactory, IUIRequestProcessor uiRequestProcessor) {
      this._standarImageSourceFactory = standarImageSourceFactory;
      this._uiRequestProcessor = uiRequestProcessor;
    }

    public IStandarImageSourceFactory StandarImageSourceFactory {
      get {
        return this._standarImageSourceFactory;
      }
    }

    public IUIRequestProcessor UIRequestProcessor {
      get {
        return this._uiRequestProcessor;
      }
    }
  }
}