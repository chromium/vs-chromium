using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.Logging;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class VsWindowFrameNotifyHandler : IVsWindowFrameNotify {
    private readonly IVsWindowFrame2 _frame;
    private uint _notifyCookie;
    private bool _isVisible;

    public VsWindowFrameNotifyHandler(IVsWindowFrame2 frame) {
      _frame = frame;
      _isVisible = true;
    }

    public bool IsVisible {
      get { return _isVisible; }
    }

    public void Advise() {
      var hr = _frame.Advise(this, out _notifyCookie);
      if (ErrorHandler.Failed(hr)) {
        Logger.LogWarning("IVsWindowFrame2.Advise() failed: hr={0}", hr);
      }
    }

    int IVsWindowFrameNotify.OnShow(int fShow) {
      var show = (__FRAMESHOW)fShow;
      switch (show) {
        case __FRAMESHOW.FRAMESHOW_Hidden:
          _isVisible = false;
          break;
        case __FRAMESHOW.FRAMESHOW_WinShown:
          _isVisible = true;
          break;
        case __FRAMESHOW.FRAMESHOW_WinClosed:
          _isVisible = false;
          var hr = _frame.Unadvise(_notifyCookie);
          if (ErrorHandler.Failed(hr)) {
            Logger.LogWarning("IVsWindowFrame2.Unadvise() failed: hr={0}", hr);
          }
          break;
        case __FRAMESHOW.FRAMESHOW_TabActivated:
        case __FRAMESHOW.FRAMESHOW_TabDeactivated:
        case __FRAMESHOW.FRAMESHOW_WinRestored:
        case __FRAMESHOW.FRAMESHOW_WinMinimized:
        case __FRAMESHOW.FRAMESHOW_WinMaximized:
        case __FRAMESHOW.FRAMESHOW_DestroyMultInst:
        case __FRAMESHOW.FRAMESHOW_AutoHideSlideBegin:
        default:
          break;
      }
      return VSConstants.S_OK;
    }

    int IVsWindowFrameNotify.OnMove() {
      return VSConstants.S_OK;
    }

    int IVsWindowFrameNotify.OnSize() {
      return VSConstants.S_OK;
    }

    int IVsWindowFrameNotify.OnDockableChange(int fDockable) {
      return VSConstants.S_OK;
    }
  }
}