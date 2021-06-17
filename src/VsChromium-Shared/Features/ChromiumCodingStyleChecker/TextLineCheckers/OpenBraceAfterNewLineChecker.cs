﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsChromium.ChromiumEnlistment;
using VsChromium.Core.Files;
using VsChromium.Settings;
using VsChromium.Views;

namespace VsChromium.Features.ChromiumCodingStyleChecker.TextLineCheckers {
  /// <summary>
  /// Check that a "{" is always at the end of a line.
  /// DISABLED for now because there are too many exceptions.
  /// </summary>
  [Export(typeof(ITextLineChecker))]
  public class OpenBraceAfterNewLineChecker : ITextLineChecker {
    private const string WhitespaceCharacters = " \t";

    [Import]
    private IChromiumSourceFiles _chromiumSourceFiles = null; // Set by MEF
    [Import]
    private IFileSystem _fileSystem = null; // Set by MEF
    [Import]
    private IGlobalSettingsProvider _globalSettingsProvider = null; // Set by MEF

    public bool AppliesToContentType(IContentType contentType) {
      return contentType.IsOfType("C/C++");
    }

    public IEnumerable<TextLineCheckerError> CheckLine(ITextSnapshotLine line) {
      if (!_globalSettingsProvider.GlobalSettings.CodingStyleOpenBraceAfterNewLine)
        yield break;

      if (!_chromiumSourceFiles.ApplyCodingStyle(_fileSystem, line))
        yield break;

        foreach (var point in line.GetFragment(line.Start, line.End, TextLineFragment.Options.Default).GetPoints()) {
          if (WhitespaceCharacters.IndexOf(point.GetChar()) >= 0) {
            // continue as long as we find whitespaces
          } else if (point.GetChar() == '{') {
            yield return new TextLineCheckerError {
              Span = new SnapshotSpan(point, point + 1),
              Message =
                "Open curly brace ('{') should always be at the end of a line, never on a new line preceeded by spaces.",
            };
          } else {
            // Stop at the first non-whitespace character.
            yield break;
          }
        }
    }
  }
}
