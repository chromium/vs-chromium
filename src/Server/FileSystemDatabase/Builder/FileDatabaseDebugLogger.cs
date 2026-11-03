// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemContents;

namespace VsChromium.Server.FileSystemDatabase.Builder {
  public static class FileDatabaseDebugLogger {
    private static readonly bool LogPiecesStats = false;

    public static void LogFilePieces(IList<FileContentsPiece> filePieces, int partitionCount) {
      if (LogPiecesStats && Logger.IsInfoEnabled) {
        Invariants.Assert(filePieces.All(x => x.FileContents != null));
        filePieces.GetPartitionRanges(partitionCount).ForAll(
          (index, range) => {
            Logger.LogInfo("Partition {0} has a size of {1}",
              index,
              FormatSizeAsKb(filePieces
                .Skip(range.Key)
                .Take(range.Value)
                .Aggregate(0L, (c, x) => c + x.ByteLength)));
          });
      }
    }

    private static string FormatSizeAsKb(long value) {
      return string.Format("{0:n0} KB", value / 1024);
    }
  }
}
