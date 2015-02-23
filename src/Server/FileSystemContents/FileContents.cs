// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  /// <summary>
  /// Abstraction over a file contents stored in (native) memory. Note that we
  /// don't store the path because the same FileContents can be shared for
  /// multiple identical files. This class and derived classes are guaranteed to
  /// be thread safe and immutable.
  /// </summary>
  public abstract class FileContents {
    protected const int MaxLineExtentOffset = 1024;

    protected static List<FilePositionSpan> NoSpans = new List<FilePositionSpan>();
    protected static IEnumerable<FileExtract> NoFileExtracts = Enumerable.Empty<FileExtract>();
    private readonly FileContentsMemory _contents;
    private readonly DateTime _utcLastModified;
    private readonly Lazy<FileContentsHash> _hash;

    protected FileContents(FileContentsMemory contents, DateTime utcLastModified) {
      _contents = contents;
      _utcLastModified = utcLastModified;
      _hash = new Lazy<FileContentsHash>(CreateHash);
    }

    public DateTime UtcLastModified { get { return _utcLastModified; } }

    public TextRange TextRange { get { return new TextRange(0, CharacterCount); } }

    public int ByteLength { get { return _contents.ByteLength; } }

    public abstract bool HasSameContents(FileContents other);

    public FileContentsPiece CreatePiece(FileName fileName, int fileId, TextRange range) {
      return new FileContentsPiece(fileName, this, fileId, range);
    }

    /// <summary>
    /// Find all instances of the search pattern stored in <paramref
    /// name="compiledTextSearchData"/> within the passed in <paramref
    /// name="textRange"/>
    /// </summary>
    public IList<TextRange> FindAll(
      CompiledTextSearchData compiledTextSearchData,
      TextRange textRange,
      IOperationProgressTracker progressTracker) {

      var textFragment = CreateFragmentFromRange(textRange);
      var providerForMainEntry = compiledTextSearchData
        .GetSearchContainer(compiledTextSearchData.ParsedSearchString.MainEntry);
      var textSearch = GetCompiledTextSearch(providerForMainEntry);
      var postProcessSearchHit = CreateFilterForOtherEntries(compiledTextSearchData);
      var result = textSearch.FindAll(textFragment, postProcessSearchHit, progressTracker);
      return result;
    }

    public IEnumerable<FileExtract> GetFileExtracts(int maxLength, IEnumerable<FilePositionSpan> spans) {
      var offsets = GetFileOffsets();

      return spans
        .Select(x => offsets.FilePositionSpanToFileExtract(x, maxLength))
        .Where(x => x != null)
        .ToList();
    }

    protected int CharacterCount { get { return ByteLength / CharacterSize; } }

    protected TextFragment TextFragment {
      get { return new TextFragment(this.Contents.Pointer, 0, this.CharacterCount, this.CharacterSize); }
    }

    protected abstract ITextLineOffsets GetFileOffsets();

    public abstract byte CharacterSize { get; }

    protected abstract ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchContainer container);

    protected abstract TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength);

    protected FileContentsHash Hash { get { return _hash.Value; } }

    protected FileContentsMemory Contents {
      get { return _contents; }
    }

    protected static bool CompareBinaryContents(
      FileContents item1,
      IntPtr ptr1,
      long byteSize1,
      FileContents item2,
      IntPtr ptr2,
      long byteSize2) {

      if (byteSize1 != byteSize2)
        return false;

      const int smallPrefixSize = 256;
      if (byteSize1 < smallPrefixSize) {
        return NativeMethods.Ascii_Compare(
          ptr1,
          byteSize1,
          ptr2,
          byteSize2);
      }

      if (!NativeMethods.Ascii_Compare(ptr1, smallPrefixSize, ptr2, smallPrefixSize)) {
        return false;
      }

      return item1.Hash.Equals(item2.Hash);
    }

    private FileContentsHash CreateHash() {
      return new FileContentsHash(Contents);
    }

    private TextFragment CreateFragmentFromRange(TextRange textRange) {
      return TextFragment.Sub(textRange.Position, textRange.Length);
    }

    private Func<TextRange, TextRange?> CreateFilterForOtherEntries(
      CompiledTextSearchData compiledTextSearchData) {
      if (compiledTextSearchData.ParsedSearchString.EntriesBeforeMainEntry.Count == 0 &&
          compiledTextSearchData.ParsedSearchString.EntriesAfterMainEntry.Count == 0) {
        return x => x;
      }

      // Search for a match for "entry" withing "textRange"
      FindEntryFunction findEntry = (textRange, entry) => {
        var algo = this.GetCompiledTextSearch(compiledTextSearchData.GetSearchContainer(entry));
        return algo.FindFirst(CreateFragmentFromRange(textRange), OperationProgressTracker.None);
      };

      // Return the extent of the line to look into for non-main entries.
      GetLineRangeFunction getLineRange = position =>
        this.GetLineTextRangeFromPosition(position, MaxLineExtentOffset);

      var sourceTextSearch = new TextSourceTextSearch(
        getLineRange,
        findEntry,
        compiledTextSearchData.ParsedSearchString);
      return sourceTextSearch.FilterSearchHit;
    }
  }
}
