using System;
using System.Collections.Generic;
using VsChromium.Core;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.NativeInterop {
  public abstract class UTF16StringSearchAlgorithm : IDisposable {
    public abstract int PatternLength { get; }
    public abstract bool MatchCase { get; }

    public virtual void Dispose() {
    }

    public IEnumerable<FilePositionSpan> SearchAll(IntPtr text, int textLen) {
      var currentPtr = text;
      var remainingLength = textLen;
      while (true) {
        currentPtr = Search(currentPtr, remainingLength);
        if (currentPtr == IntPtr.Zero)
          break;

        // TODO(rpaquay): We are limited to 2GB for now.
        var offset = Pointers.Offset32(text, currentPtr);
        yield return new FilePositionSpan { Position = offset, Length = PatternLength };
        currentPtr += PatternLength * sizeof(char);
        remainingLength = textLen - offset - (PatternLength * sizeof(char));
      }
    }

    public abstract IntPtr Search(IntPtr text, int textLen);
  }
}