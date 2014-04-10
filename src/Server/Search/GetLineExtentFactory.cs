using System;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Search {
  public class GetLineExtentFactory {
    private readonly Func<int, FilePositionSpan> _getLineExtent;
    private FilePositionSpan? _previousSpan;

    public GetLineExtentFactory(Func<int, FilePositionSpan> getLineExtent) {
      _getLineExtent = getLineExtent;
    }

    public FilePositionSpan GetLineExtent(int position) {
      if (_previousSpan.HasValue) {
        if (position >= _previousSpan.Value.Position &&
            position < _previousSpan.Value.Position + _previousSpan.Value.Length) {
          return _previousSpan.Value;
        }
      }

      _previousSpan = _getLineExtent(position);
      return _previousSpan.Value;
    }
  }
}