// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsChromium.ChromiumEnlistment;
using VsChromium.Core.Files;
using VsChromium.Views;

namespace VsChromium.Features.ChromiumCodingStyleChecker.TextLineCheckers {
  /// <summary>
  /// Check that end of line characters are LF only (not CRLF or CR).
  /// </summary>
  [Export(typeof(ITextLineChecker))]
  public class EndOfLineChecker : ITextLineChecker {
    [Import]
    private IChromiumSourceFiles _chromiumSourceFiles = null; // Set by MEF
    [Import]
    private IFileSystem _fileSystem = null; // Set by MEF

    public bool AppliesToContentType(IContentType contentType) {
      return contentType.IsOfType("text");
    }

    public IEnumerable<TextLineCheckerError> CheckLine(ITextSnapshotLine line) {
      if (_chromiumSourceFiles.ApplyCodingStyle(_fileSystem, line)) {
        var lineBreak = line.GetLineBreakText();
        if (lineBreak.Length > 0 && lineBreak != "\n") {
          var fragment = line.GetFragment(line.End.Position - 1, line.EndIncludingLineBreak.Position,
                                          TextLineFragment.Options.IncludeLineBreak);
          yield return new TextLineCheckerError {
            Span = fragment.SnapshotSpan,
            Message = "Line breaks should be \"unix\" (i.e. LF) style only.",
          };
        }
      }
    }
  }
}
