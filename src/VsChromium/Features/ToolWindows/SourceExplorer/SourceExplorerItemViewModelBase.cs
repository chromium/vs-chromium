using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class SourceExplorerItemViewModelBase : TreeViewItemViewModel {
    private IUIRequestProcessor _uiRequestProcessor;

    public SourceExplorerItemViewModelBase(
        IUIRequestProcessor uiRequestProcessor,
        IStandarImageSourceFactory imageSourceFactory,
        TreeViewItemViewModel parent,
        bool lazyLoadChildren) 
      : base(imageSourceFactory, parent, lazyLoadChildren) {
      _uiRequestProcessor = uiRequestProcessor;
    }

    public IUIRequestProcessor UIRequestProcessor { get { return _uiRequestProcessor; } }
  }
}
