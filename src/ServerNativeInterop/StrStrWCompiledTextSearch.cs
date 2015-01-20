using System;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Server.NativeInterop {
  public class StrStrWCompiledTextSearch : Utf16CompiledTextSearch {
    private readonly SafeHGlobalHandle _patternPtr;
    private readonly int _patternLength;
    private readonly NativeMethods.SearchOptions _searchOptions;

    public StrStrWCompiledTextSearch(string pattern, NativeMethods.SearchOptions searchOptions) {
      _patternPtr = new SafeHGlobalHandle(Marshal.StringToHGlobalUni(pattern));
      _patternLength = pattern.Length;
      _searchOptions = searchOptions;
    }

    public override void Dispose() {
      _patternPtr.Dispose();
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

    public override TextFragment Search(TextFragment textFragment) {
      var searchHitPtr = MatchCase
        ? StrStrW(textFragment.FragmentStart, _patternPtr.Pointer)
        : StrStrIW(textFragment.FragmentStart, _patternPtr.Pointer);
      if (searchHitPtr == IntPtr.Zero)
        return TextFragment.Null;
      return textFragment.Sub(searchHitPtr, _patternLength);
    }

    public override int PatternLength {
      get { return _patternLength; }
    }
  }
}