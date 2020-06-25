// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
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

    protected FileContents(FileContentsMemory contents, DateTime utcLastModified) {
      _contents = contents;
      _utcLastModified = utcLastModified;
    }

    public DateTime UtcLastModified { get { return _utcLastModified; } }

    public TextRange TextRange { get { return new TextRange(0, CharacterCount); } }

    public int ByteLength { get { return _contents.ByteLength; } }

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
        .GetSearchContainer(compiledTextSearchData.ParsedSearchString.LongestEntry);
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
      get { return new TextFragment(Contents.Pointer, 0, CharacterCount, CharacterSize); }
    }

    protected abstract ITextLineOffsets GetFileOffsets();

    public abstract byte CharacterSize { get; }

    protected abstract ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchContainer container);

    protected abstract TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength);

    protected FileContentsMemory Contents {
      get { return _contents; }
    }

    private TextFragment CreateFragmentFromRange(TextRange textRange) {
      return TextFragment.Sub(textRange.Position, textRange.Length);
    }

    private Func<TextRange, TextRange?> CreateFilterForOtherEntries(
      CompiledTextSearchData compiledTextSearchData) {
      if (compiledTextSearchData.ParsedSearchString.EntriesBeforeLongestEntry.Count == 0 &&
          compiledTextSearchData.ParsedSearchString.EntriesAfterLongestEntry.Count == 0) {
        return x => x;
      }

      // Search for a match for "entry" withing "textRange"
      FindEntryFunction findEntry = (textRange, entry) => {
        var algo = GetCompiledTextSearch(compiledTextSearchData.GetSearchContainer(entry));
        return algo.FindFirst(CreateFragmentFromRange(textRange), OperationProgressTracker.None);
      };

      // Return the extent of the line to look into for non-main entries.
      GetLineRangeFunction getLineRange = position =>
        GetLineTextRangeFromPosition(position, MaxLineExtentOffset);

      var sourceTextSearch = new TextSourceTextSearch(
        getLineRange,
        findEntry,
        compiledTextSearchData.ParsedSearchString);
      return sourceTextSearch.FilterSearchHit;
    }
  }
}
