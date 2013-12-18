// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsChromiumPackage.ChromiumEnlistment;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.Features.ChromiumCodingStyleChecker.TextLineCheckers {
  /// <summary>
  /// Check that there are no trailing spaces in code files.
  /// </summary>
  [Export(typeof(ITextLineChecker))]
  public class TrailingSpacesChecker : ITextLineChecker {
    private const string _whitespaceCharacters = " \t";

    [Import]
    private IChromiumSourceFiles _chromiumSourceFiles = null; // Set by MEF

    public bool AppliesToContentType(IContentType contentType) {
      return contentType.IsOfType("code");
    }

    public IEnumerable<TextLineCheckerError> CheckLine(ITextSnapshotLine line) {
      if (_chromiumSourceFiles.ApplyCodingStyle(line)) {
        foreach (var point in line.GetFragment(line.Start, line.End, TextLineFragment.Options.Reverse).GetPoints()) {
          if (_whitespaceCharacters.IndexOf(point.GetChar()) >= 0) {
            yield return new TextLineCheckerError {
              Span = new SnapshotSpan(point, point + 1),
              Message = "Trailing whitespaces are not allowed.",
            };
          } else
            yield break;
        }
      }
    }
  }
}
