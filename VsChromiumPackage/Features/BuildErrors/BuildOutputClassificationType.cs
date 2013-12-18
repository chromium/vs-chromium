// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumPackage.Features.BuildErrors {
  /// <summary>
  /// Defines the visual format for the classification type
  /// </summary>
  [Export(typeof(EditorFormatDefinition))]
  [Name(BuildOutputConstants.ClassifierName)]
  [ClassificationType(ClassificationTypeNames = BuildOutputConstants.ClassifierName)]
  [UserVisible(true)] // This should be visible to the end user
  [Order(Before = Priority.Default)] // Sets the priority to be after the default classifiers
  public class BuildOutputClassificationType : ClassificationFormatDefinition {
    public BuildOutputClassificationType() {
      DisplayName = BuildOutputConstants.ClassifierDisplayName;
      ForegroundColor = BuildOutputConstants.ClassifierForegroundColor;
      TextDecorations = BuildOutputConstants.ClassifierTextDecorations;
    }
  }
}
