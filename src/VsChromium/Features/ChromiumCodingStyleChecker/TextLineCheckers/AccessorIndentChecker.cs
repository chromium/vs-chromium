// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsChromium.ChromiumEnlistment;
using VsChromium.Core.FileNames;
using VsChromium.Views;

namespace VsChromium.Features.ChromiumCodingStyleChecker.TextLineCheckers {
  /// <summary>
  /// Checks that "public:", etc. accessors are indented by an odd number of spaces.
  /// </summary>
  [Export(typeof(ITextLineChecker))]
  public class AccessorIndentChecker : ITextLineChecker {
    private const string WhitespaceCharacters = " \t";

    [Import]
    private IChromiumSourceFiles _chromiumSourceFiles = null; // Set by MEF
    [Import]
    private IFileSystem _fileSystem = null; // Set by MEF

    public bool AppliesToContentType(IContentType contentType) {
      return contentType.IsOfType("C/C++");
    }

    public IEnumerable<TextLineCheckerError> CheckLine(ITextSnapshotLine line) {
      if (_chromiumSourceFiles.ApplyCodingStyle(_fileSystem, line)) {
        int indent = 0;
        var fragment = line.GetFragment(line.Start, line.End, TextLineFragment.Options.Default);
        foreach (var point in fragment.GetPoints()) {
          if (WhitespaceCharacters.IndexOf(point.GetChar()) >= 0) {
            // continue as long as we find whitespaces
            indent++;
          } else if (GetMarker(line, fragment, point) != null) {
            if (indent % 2 == 0) // even indentation is not ok
            {
              var marker = GetMarker(line, fragment, point);
              yield return new TextLineCheckerError {
                Span = new SnapshotSpan(point, marker.Length),
                Message =
                  string.Format("Accessor \"{0}\" should always be indented 1 character less than rest of class body.",
                                marker)
              };
            }
          } else {
            // Stop at the first non-whitespace character.
            yield break;
          }
        }
      }
    }

    private string GetMarker(ITextSnapshotLine line, TextLineFragment fragment, SnapshotPoint point) {
      string[] markers = {
        "private:",
        "public:",
        "protected:"
      };

      return markers
        .Where(marker => fragment.GetText(point - line.Start, marker.Length) == marker)
        .FirstOrDefault();
    }
  }
}
