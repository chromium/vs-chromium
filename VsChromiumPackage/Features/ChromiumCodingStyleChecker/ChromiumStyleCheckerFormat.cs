// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumPackage.Features.ChromiumCodingStyleChecker {
  /// <summary>
  /// Defines the format of the classification type.
  /// </summary>
  [Export(typeof(EditorFormatDefinition))]
  [Name("VsChromiumPackageStyleChecker")]
  [ClassificationType(ClassificationTypeNames = "VsChromiumPackageStyleChecker")]
  [UserVisible(true)] //this should be visible to the end user
  [Order(Before = Priority.Default)] //set the priority to be after the default classifiers
  public class VsChromiumPackageFormat : ClassificationFormatDefinition {
    /// <summary>
    /// Defines the visual format for the classification type
    /// </summary>
    public VsChromiumPackageFormat() {
      DisplayName = "VsChromiumPackageStyleChecker"; //human readable version of the name
      BackgroundColor = Colors.Red;
      TextDecorations = System.Windows.TextDecorations.Underline;
    }
  }
}
