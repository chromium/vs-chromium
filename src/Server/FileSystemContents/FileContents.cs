// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  /// <summary>
  /// Abstraction over a file contents
  /// </summary>
  public abstract class FileContents {
    protected static List<FilePositionSpan> NoSpans = new List<FilePositionSpan>();
    protected static IEnumerable<FileExtract> NoFileExtracts = Enumerable.Empty<FileExtract>();
    private readonly DateTime _utcLastModified;

    protected FileContents(DateTime utcLastModified) {
      _utcLastModified = utcLastModified;
    }

    public DateTime UtcLastModified { get { return _utcLastModified; } }

    public abstract long ByteLength { get; }

    public abstract bool HasSameContents(FileContents other);

    public virtual List<FilePositionSpan> Search(
        FileName fileName,
        SearchContentsData searchContentsData,
        IOperationProgressTracker progressTracker) {
      return NoSpans;
    }

    public virtual IEnumerable<FileExtract> GetFileExtracts(IEnumerable<FilePositionSpan> spans) {
      return NoFileExtracts;
    }
  }
}
