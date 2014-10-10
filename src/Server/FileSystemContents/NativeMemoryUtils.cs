using System;

namespace VsChromium.Server.FileSystemContents {
  public class NativeMemoryUtils {
    // Copyright (c) 2008-2013 Hafthor Stefansson
    // Distributed under the MIT/X11 software license
    // Ref: http://www.opensource.org/licenses/mit-license.php.
    public static unsafe bool UnsafeCompare(IntPtr a1, long l1, IntPtr a2, long l2) {
      if (a1 == IntPtr.Zero || a2 == IntPtr.Zero || l1 != l2)
        return false;

      var x1 = (byte*) a1.ToPointer();
      var x2 = (byte*) a2.ToPointer();
      long l = l1;
      for (long i = 0; i < l/8; i++, x1 += 8, x2 += 8) {
        if (*((long*) x1) != *((long*) x2)) return false;
      }

      if ((l & 4) != 0) {
        if (*((int*) x1) != *((int*) x2)) return false;
        x1 += 4;
        x2 += 4;
      }

      if ((l & 2) != 0) {
        if (*((short*) x1) != *((short*) x2)) return false;
        x1 += 2;
        x2 += 2;
      }
      if ((l & 1) != 0) {
        if (*((byte*) x1) != *((byte*) x2)) return false;
      }
      return true;
    }
  }
}