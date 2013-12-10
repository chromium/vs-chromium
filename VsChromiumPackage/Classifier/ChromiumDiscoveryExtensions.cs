// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;
using Microsoft.VisualStudio.Text;
using VsChromiumCore.FileNames;
using VsChromiumPackage.ChromiumEnlistment;

namespace VsChromiumPackage.Classifier {
  public static class ChromiumSourceFilesExtensions {
    public static bool ApplyCodingStyle(this IChromiumSourceFiles chromiumSourceFiles, ITextSnapshotLine line) {
      // Check document is part of a Chromium source repository
      ITextDocument document;
      if (!line.Snapshot.TextBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out document))
        return false;

      var path = document.FilePath;
      if (!PathHelpers.IsAbsolutePath(path))
        return false;

      if (!File.Exists(path))
        return false;

      return chromiumSourceFiles.ApplyCodingStyle(document.FilePath);
    }
  }
}
