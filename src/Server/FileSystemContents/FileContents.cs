// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.TextSearch;
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

    /// <summary>
    /// Maximum length of a chunk (piece) of a file: files larger than this (in bytes) are split in
    /// chunks of at most this number of characters (see <see cref="GetChunks"/>).
    /// </summary>
    public const int MaxChunkLength = 100 * 1024;

    /// <summary>
    /// Note: Built in the background after the file contents is loaded (see
    /// <see cref="BuildTrigramFilters"/>): null until then (no filtering).
    /// </summary>
    private volatile TrigramFilterSet _trigramFilters;

    public FileContentsPiece CreatePiece(FileName fileName, int fileId, TextRange range, int chunkIndex = 0) {
      return new FileContentsPiece(fileName, this, fileId, range, chunkIndex);
    }

    /// <summary>
    /// The chunks of text of this file, i.e. the ranges of its pieces: the whole text if the file
    /// is not larger than <see cref="MaxChunkLength"/> bytes, otherwise chunks of at most <see
    /// cref="MaxChunkLength"/> characters ending with a new line when possible.
    /// </summary>
    public IReadOnlyList<TextRange> GetChunks() {
      if (ByteLength <= MaxChunkLength) {
        return new[] { TextRange };
      }
      var result = new List<TextRange>();
      var range = TextRange;
      while (range.Length > 0) {
        var chunkLength = GetChunkLength(range.Position, MaxChunkLength);
        var chunk = new TextRange(range.Position, chunkLength);
        result.Add(chunk);
        range = new TextRange(chunk.EndPosition, range.EndPosition - chunk.EndPosition);
      }
      return result;
    }

    /// <summary>
    /// Computes the trigram filters of the chunks of this file (see <see cref="TrigramFilters"/>),
    /// if not done yet. Thread safe: searches running concurrently use the filters once they are
    /// published (atomic reference assignment).
    /// </summary>
    public void BuildTrigramFilters() {
      if (TrigramFilters.Enabled && ByteLength > 0 && _trigramFilters == null) {
        _trigramFilters = CreateTrigramFilters(GetChunks());
      }
    }

    /// <summary>
    /// Memory used by the trigram filters of this file (0 if none).
    /// </summary>
    public long TrigramFiltersByteSize => _trigramFilters?.ByteSize ?? 0;

    /// <summary>
    /// Releases the trigram filters of this file (e.g. disabled for its project), and returns the
    /// memory they used. Searches running concurrently scan the pieces without filters.
    /// </summary>
    public long DropTrigramFilters() {
      var filters = Interlocked.Exchange(ref _trigramFilters, null);
      return filters?.ByteSize ?? 0;
    }

    public bool HasTrigramFilters => _trigramFilters != null;

    protected virtual TrigramFilterSet CreateTrigramFilters(IReadOnlyList<TextRange> chunks) {
      return null;
    }

    /// <summary>
    /// Returns false if the chunk <paramref name="chunkIndex"/> (see <see cref="GetChunks"/>)
    /// certainly doesn't contain a match of the search of <paramref name="query"/>.
    /// </summary>
    public bool MayContain(int chunkIndex, TrigramQuery query) {
      var filters = _trigramFilters;
      if (filters == null)
        return true;
      if (!IsAsciiText && !query.AppliesToNonAsciiText)
        return true;
      return filters.MayContain(chunkIndex, query);
    }

    /// <summary>
    /// Find all instances of the search pattern stored in <paramref
    /// name="compiledTextSearchData"/> within the passed in <paramref
    /// name="textRange"/>. The ranges returned are UTF-16 positions (see <see
    /// cref="ToUtf16Ranges"/>).
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
      return ToUtf16Ranges(result);
    }

    /// <summary>
    /// Converts ranges of the text (e.g. UTF-8 byte positions) to ranges of the UTF-16
    /// representation of the text, i.e. the positions exchanged with clients.
    /// </summary>
    protected virtual IList<TextRange> ToUtf16Ranges(IList<TextRange> ranges) {
      return ranges;
    }

    /// <summary>
    /// True if the text contains only ASCII characters.
    /// </summary>
    protected virtual bool IsAsciiText => true;

    /// <summary>
    /// Returns the start of the character containing <paramref name="position"/> (positions in
    /// the middle of a multi-byte character are moved to the start of the character).
    /// </summary>
    protected virtual int GetCharacterBoundary(int position) {
      return position;
    }

    /// <summary>
    /// Returns the length of the chunk of text starting at <paramref
    /// name="position"/>, of at most <paramref name="maxLength"/> characters.
    /// When possible, chunks end with a new line character, so that search
    /// matches never span multiple chunks (matches can't span multiple
    /// lines, except for some regular expressions).
    /// </summary>
    public int GetChunkLength(int position, int maxLength) {
      var remaining = CharacterCount - position;
      if (remaining <= maxLength)
        return remaining;
      var newLinePosition = LastIndexOfNewLine(new TextRange(position, maxLength));
      if (newLinePosition < 0) {
        // Don't split a character
        var end = GetCharacterBoundary(position + maxLength);
        return end > position ? end - position : maxLength;
      }
      return newLinePosition - position + 1;
    }

    /// <summary>
    /// Returns the extracts of the text at the (UTF-16) positions <paramref name="spans"/>.
    /// </summary>
    public virtual IEnumerable<FileExtract> GetFileExtracts(int maxLength, IEnumerable<FilePositionSpan> spans) {
      var offsets = GetFileOffsets();

      return spans
        .Select(x => offsets.FilePositionSpanToFileExtract(x, maxLength))
        .Where(x => x != null)
        .ToList();
    }

    /// <summary>
    /// The length of the text, in bytes (UTF-8).
    /// </summary>
    protected int CharacterCount { get { return ByteLength; } }

    protected TextFragment TextFragment {
      get { return new TextFragment(Contents.Pointer, 0, CharacterCount, sizeof(byte)); }
    }

    protected abstract ITextLineOffsets GetFileOffsets();

    protected abstract ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchContainer container);

    protected abstract TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength);

    /// <summary>
    /// Returns the position of the last new line character in <paramref
    /// name="range"/>, or -1 if there is none.
    /// </summary>
    protected abstract int LastIndexOfNewLine(TextRange range);

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
