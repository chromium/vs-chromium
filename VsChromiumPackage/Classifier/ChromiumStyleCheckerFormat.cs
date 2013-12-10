// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumPackage.Classifier {
  /// <summary>
  /// Defines an editor format for the VsChromiumPackage type that has a purple background
  /// and is underlined.
  /// </summary>
  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = "VsChromiumPackage")]
  [Name("VsChromiumPackage")]
  [UserVisible(true)] //this should be visible to the end user
  [Order(Before = Priority.Default)] //set the priority to be after the default classifiers
  sealed class VsChromiumPackageFormat : ClassificationFormatDefinition {
    /// <summary>
    /// Defines the visual format for the "VsChromiumPackage" classification type
    /// </summary>
    public VsChromiumPackageFormat() {
      DisplayName = "VsChromiumPackage"; //human readable version of the name
      BackgroundColor = Colors.Red;
      TextDecorations = System.Windows.TextDecorations.Underline;
    }
  }
}
