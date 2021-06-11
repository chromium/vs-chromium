// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using VsChromium.Core.Configuration;

namespace VsChromium.Features.ChromiumCodingStyleChecker {
  /// <summary>
  /// This class causes a classifier to be added to the set of classifiers. Since 
  /// the content type is set to "text", this classifier applies to all text files
  /// </summary>
  [Export(typeof(IClassifierProvider))]
  [ContentType("text")]
  class ChromiumStyleClassifierProvider : IClassifierProvider {
    /// <summary>
    /// Import the classification registry to be used for getting a reference
    /// to the custom classification type later.
    /// </summary>
    [Import]
    internal IClassificationTypeRegistryService ClassificationRegistry = null; // Set via MEF

    [Import]
    internal IConfigurationFileLocator ConfigurationFileLocator = null; // Set via MEF

    [ImportMany]
    internal IEnumerable<ITextLineChecker> TextLineCheckers = null; // Set via MEF

    public IClassifier GetClassifier(ITextBuffer buffer) {
      // Ensure VS Package is loaded, as classifier implementation depends on services
      // and initialization code provider by the implementation.
      VsPackage.EnsureLoaded();

      return
        buffer.Properties.GetOrCreateSingletonProperty<ChromiumStyleClassifier>(
          () =>
          new ChromiumStyleClassifier(ClassificationRegistry, TextLineCheckers,
                                 ConfigurationFileLocator));
    }
  }
}
