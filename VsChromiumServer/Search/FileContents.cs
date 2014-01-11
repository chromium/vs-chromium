// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumServer.Search {
  public abstract class FileContents {
    protected static List<int> NoPositions = new List<int>();
    protected static IEnumerable<FileExtract> NoFileExtracts = Enumerable.Empty<FileExtract>();
    private readonly DateTime _utcLastWriteTime;

    protected FileContents(DateTime utcLastWriteTime) {
      _utcLastWriteTime = utcLastWriteTime;
    }

    public DateTime UtcLastModified { get { return _utcLastWriteTime; } }

    public abstract long ByteLength { get; }

    public virtual List<int> Search(SearchContentsData searchContentsData) {
      return NoPositions;
    }

    public virtual IEnumerable<FileExtract> GetFileExtracts(IEnumerable<FilePositionSpan> spans) {
      return NoFileExtracts;
    }
  }
}
