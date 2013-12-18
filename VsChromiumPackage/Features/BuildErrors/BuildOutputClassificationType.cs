// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumPackage.Features.BuildErrors {
  [Export(typeof(EditorFormatDefinition))]
  [Name("VsChromiumPackageBuildOutput")]
  [ClassificationType(ClassificationTypeNames = "VsChromiumPackageBuildOutput")]
  [UserVisible(true)] // This should be visible to the end user
  [Order(Before = Priority.Default)] // Sets the priority to be after the default classifiers
  public class BuildOutputClassificationType : ClassificationFormatDefinition {
    /// <summary>
    /// Defines the visual format for the classification type
    /// </summary>
    public BuildOutputClassificationType() {
      DisplayName = "VsChromiumPackageBuildOutput"; // Human readable version of the name
      ForegroundColor = Colors.Blue;
      TextDecorations = System.Windows.TextDecorations.Underline;
    }
  }
}
