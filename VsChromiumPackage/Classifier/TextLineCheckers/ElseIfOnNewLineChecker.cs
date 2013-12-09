// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsChromiumPackage.ChromiumEnlistment;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.Classifier.TextLineCheckers {
  /// <summary>
  /// Check that a "else" of "else if" is always on the same line as the "}" of the if statetement:
  /// 
  ///  if (foo) {
  ///  } else if {
  ///  }
  /// 
  /// </summary>
  [Export(typeof(ITextLineChecker))]
  public class ElseIfOnNewLineChecker : ITextLineChecker {
    private const string _whitespaceCharacters = " \t";

    [Import]
    private IChromiumSourceFiles _chromiumSourceFiles = null; // Set by MEF

    public bool AppliesToContentType(IContentType contentType) {
      return contentType.IsOfType("C/C++");
    }

    public IEnumerable<TextLineCheckerError> CheckLine(ITextSnapshotLine line) {
      if (this._chromiumSourceFiles.ApplyCodingStyle(line)) {
        var fragment = line.GetFragment(line.Start, line.End, TextLineFragment.Options.Default);
        foreach (var point in fragment.GetPoints()) {
          if (_whitespaceCharacters.IndexOf(point.GetChar()) >= 0) {
            // continue as long as we find whitespaces
          } else if (GetMarker(line, fragment, point) != null) {
            var marker = GetMarker(line, fragment, point);
            yield return new TextLineCheckerError {
              Span = new SnapshotSpan(point, marker.Length),
              Message = string.Format("\"{0}\" should always be on the same line as the \"}}\" character.", marker)
            };
          } else {
            // Stop at the first non-whitespace character.
            yield break;
          }
        }
      }
    }

    private string GetMarker(ITextSnapshotLine line, TextLineFragment fragment, SnapshotPoint point) {
      string[] markers = {
        "else",
        "else if",
      };

      var match = markers
          .Where(marker => fragment.GetText(point - line.Start, marker.Length) == marker)
          .FirstOrDefault();
      if (match != null) {
        // If last character of line is not "{", we are good
        var end = line.GetFragment(line.End.Position - 1, line.End.Position, TextLineFragment.Options.Default);
        if (end.GetText() != "{")
          return null;
      }
      return match;
    }
  }
}
