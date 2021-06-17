// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VsChromium.Features.BuildOutputAnalyzer {
  /// <summary>
  /// Defines the visual format for the classification type
  /// </summary>
  [Export(typeof(EditorFormatDefinition))]
  [Name(BuildOutputClassifierConstants.Name)]
  [ClassificationType(ClassificationTypeNames = BuildOutputClassifierConstants.Name)]
  [UserVisible(true)] // This should be visible to the end user
  [Order(Before = Priority.Default)] // Sets the priority to be after the default classifiers
  public class BuildOutputClassificationType : ClassificationFormatDefinition {
    public BuildOutputClassificationType() {
      DisplayName = BuildOutputClassifierConstants.DisplayName;
      ForegroundColor = BuildOutputClassifierConstants.ForegroundColor;
      TextDecorations = BuildOutputClassifierConstants.TextDecorations;
    }
  }
}
