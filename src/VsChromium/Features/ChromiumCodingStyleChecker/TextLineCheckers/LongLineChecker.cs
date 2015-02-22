// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsChromium.ChromiumEnlistment;
using VsChromium.Core.Files;
using VsChromium.Settings;
using VsChromium.Views;

namespace VsChromium.Features.ChromiumCodingStyleChecker.TextLineCheckers {
  /// <summary>
  /// Check that lines are no longer than 80 characters.
  /// </summary>
  [Export(typeof(ITextLineChecker))]
  public class LongLineChecker : ITextLineChecker {
    [Import]
    private IChromiumSourceFiles _chromiumSourceFiles = null; // Set by MEF
    [Import]
    private IFileSystem _fileSystem = null; // Set by MEF
    [Import]
    private IGlobalSettingsProvider _globalSettingsProvider = null; // Set by MEF

    public bool AppliesToContentType(IContentType contentType) {
      return contentType.IsOfType("code");
    }

    public IEnumerable<TextLineCheckerError> CheckLine(ITextSnapshotLine line) {
      if (!_globalSettingsProvider.GlobalSettings.CodingStyleLongLine)
        yield break;

      if (!_chromiumSourceFiles.ApplyCodingStyle(_fileSystem, line))
        yield break;

        if (line.Length > 80) {
          if (!IsAllowedOverflow(line)) {
            yield return new TextLineCheckerError {
              Span = new SnapshotSpan(line.Start + 80, line.End),
              Message = "Maximum length of line is 80 characters.",
            };
          }
      }
    }

    private bool IsAllowedOverflow(ITextSnapshotLine line) {
      var keywords = new string[] {
        "#include",
        "#define",
        "#if",
        "#endif",
      };
      var text =
        line.GetFragment(line.Start.Position, line.Start.Position + 30, TextLineFragment.Options.Default)
          .SnapshotSpan.GetText();
      return keywords.Any(k => text.Contains(k));
    }
  }
}
