// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsChromiumPackage.ChromiumEnlistment;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.Classifier.TextLineCheckers {
  /// <summary>
  /// Check that there are no TAB characters.
  /// </summary>
  [Export(typeof(ITextLineChecker))]
  public class TabCharacterChecker : ITextLineChecker {
    [Import]
    private IChromiumSourceFiles _chromiumSourceFiles = null; // Set by MEF

    public bool AppliesToContentType(IContentType contentType) {
      return contentType.IsOfType("code");
    }

    public IEnumerable<TextLineCheckerError> CheckLine(ITextSnapshotLine line) {
      if (_chromiumSourceFiles.ApplyCodingStyle(line)) {
        if (line.Length == 0) {
          yield break;
        }

        foreach (var point in line.GetFragment(line.Start, line.End, TextLineFragment.Options.Default).GetPoints()) {
          if (point.GetChar() == '\t') {
            yield return new TextLineCheckerError {
              Span = new SnapshotSpan(point, point + 1),
              Message = "TAB characters are not allowed."
            };
          }
        }
      }
    }
  }
}
