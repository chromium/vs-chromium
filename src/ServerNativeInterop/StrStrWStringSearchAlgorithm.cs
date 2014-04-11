using System;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Server.NativeInterop {
  public class StrStrWStringSearchAlgorithm : UTF16StringSearchAlgorithm {
    private readonly SafeHGlobalHandle _searchTextUniPtr;
    private readonly NativeMethods.SearchOptions _searchOptions;
    private readonly int _patternLength;

    public StrStrWStringSearchAlgorithm(string pattern, NativeMethods.SearchOptions searchOptions) {
      _searchTextUniPtr = new SafeHGlobalHandle(Marshal.StringToHGlobalUni(pattern));
      _searchOptions = searchOptions;
      _patternLength = pattern.Length;
    }

    public override void Dispose() {
      _searchTextUniPtr.Dispose();
      base.Dispose();
    }

    private bool MatchCase {
      get {
        return (_searchOptions & NativeMethods.SearchOptions.kMatchCase) != 0;
      }
    }

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    public static extern IntPtr StrStrIW(IntPtr pszFirst, IntPtr pszSrch);

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    public static extern IntPtr StrStrW(IntPtr pszFirst, IntPtr pszSrch);

    public override IntPtr Search(IntPtr text, int textLen) {
      if (MatchCase)
        return StrStrW(text, _searchTextUniPtr.Pointer);
      else
        return StrStrIW(text, _searchTextUniPtr.Pointer);
    }

    public override int PatternLength {
      get { return _patternLength; }
    }
  }
}