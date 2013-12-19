// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumPackage.Features.ChromiumCodingStyleChecker {
  /// <summary>
  /// Defines the format of the classification type.
  /// </summary>
  [Export(typeof(EditorFormatDefinition))]
  [Name(ChromiumStyleClassifierConstants.Name)]
  [ClassificationType(ClassificationTypeNames = ChromiumStyleClassifierConstants.Name)]
  [UserVisible(true)] //this should be visible to the end user
  [Order(Before = Priority.Default)] //set the priority to be after the default classifiers
  public class ChromiumStyleClassificationType : ClassificationFormatDefinition {
    /// <summary>
    /// Defines the visual format for the classification type
    /// </summary>
    public ChromiumStyleClassificationType() {
      DisplayName = ChromiumStyleClassifierConstants.DisplayName;
      BackgroundColor = ChromiumStyleClassifierConstants.BackgroundColor;
      TextDecorations = ChromiumStyleClassifierConstants.TextDecorations;
    }
  }
}
