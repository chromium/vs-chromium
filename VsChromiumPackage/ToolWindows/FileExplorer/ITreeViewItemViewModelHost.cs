using VsChromiumPackage.Threads;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  public interface ITreeViewItemViewModelHost {
    IStandarImageSourceFactory StandarImageSourceFactory { get; }
    IUIRequestProcessor UIRequestProcessor { get; }
  }
}