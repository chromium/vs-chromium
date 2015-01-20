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
  /// Abstraction over a file contents
  /// </summary>
  public abstract class FileContents {
    protected const int MaxLineExtentOffset = 1024;

    protected static List<FilePositionSpan> NoSpans = new List<FilePositionSpan>();
    protected static IEnumerable<FileExtract> NoFileExtracts = Enumerable.Empty<FileExtract>();
    private readonly DateTime _utcLastModified;

    protected FileContents(DateTime utcLastModified) {
      _utcLastModified = utcLastModified;
    }

    public DateTime UtcLastModified { get { return _utcLastModified; } }

    public abstract int CharacterSize { get; }

    public abstract long ByteLength { get; }

    protected abstract long CharacterCount { get; }

    public abstract bool HasSameContents(FileContents other);

    protected abstract TextFragment TextFragment { get; }

    public TextRange TextRange {
      get {
        return new TextRange(0, CharacterCount);
      }
    }

    protected abstract ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchProvider provider);
    protected abstract TextRange GetLineTextRangeFromPosition(long position, long maxRangeLength);

    public FileContentsPiece CreatePiece(FileName fileName, int fileId, TextRange range) {
      return new FileContentsPiece(fileName, this, fileId, range);
    }

    public List<FilePositionSpan> Search(
      TextRange textRange,
      CompiledTextSearchData compiledTextSearchData,
      IOperationProgressTracker progressTracker) {

      // Note: In some case, textRange may be outside of our bounds. This is
      // because FileContents and FileContentsPiece may be out of date wrt to
      // each other, see FileData.UpdateContents method.
      var textFragment = CreateFragmentFromRange(textRange);

      var providerForMainEntry = compiledTextSearchData.GetSearchAlgorithmProvider(compiledTextSearchData.ParsedSearchString.MainEntry);
      var algo = this.GetCompiledTextSearch(providerForMainEntry);
      // TODO(rpaquay): We are limited to 2GB for now.
      var result = algo.SearchAll(textFragment, progressTracker);
      if (compiledTextSearchData.ParsedSearchString.EntriesBeforeMainEntry.Count == 0 &&
          compiledTextSearchData.ParsedSearchString.EntriesAfterMainEntry.Count == 0) {
        return result.ToList();
      }

      return FilterOnOtherEntries(compiledTextSearchData, result).ToList();
    }

    private TextFragment CreateFragmentFromRange(TextRange textRange) {
      var fullFragment = this.TextFragment;
      var offset = Math.Min(textRange.CharacterOffset, fullFragment.CharacterCount);
      var count = Math.Min(textRange.CharacterCount, fullFragment.CharacterCount - offset);
      var textFragment = this.TextFragment.Sub(offset, count);
      return textFragment;
    }

    private unsafe IEnumerable<FilePositionSpan> FilterOnOtherEntries(CompiledTextSearchData compiledTextSearchData, IEnumerable<FilePositionSpan> matches) {
      FindEntryFunction findEntry = (textRange, entry) => {
        var algo = this.GetCompiledTextSearch(compiledTextSearchData.GetSearchAlgorithmProvider(entry));
        var position = algo.SearchOne(CreateFragmentFromRange(textRange), OperationProgressTracker.None);
        if (!position.HasValue)
          return null;
        return new TextRange(position.Value.Position, position.Value.Length);
      };
      GetLineRangeFunction getLineRange = position => this.GetLineTextRangeFromPosition(position, MaxLineExtentOffset);

      return new TextSourceTextSearch(getLineRange, findEntry)
          .FilterOnOtherEntries(compiledTextSearchData.ParsedSearchString, matches);
    }

    public virtual IEnumerable<FileExtract> GetFileExtracts(IEnumerable<FilePositionSpan> spans) {
      return NoFileExtracts;
    }
  }
}
