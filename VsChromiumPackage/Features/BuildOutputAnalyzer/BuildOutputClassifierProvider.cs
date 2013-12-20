// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using VsChromiumCore.Configuration;

namespace VsChromiumPackage.Features.BuildOutputAnalyzer {
  /// <summary>
  /// This class causes a classifier to be added to the set of classifiers. Since 
  /// the content type is set to "text", this classifier applies to all text files
  /// </summary>
  [Export(typeof(IClassifierProvider))]
  [ContentType(NugetConsoleViewConstants.ContentType)]
  class BuildOutputClassifierProvider : IClassifierProvider {
    /// <summary>
    /// Import the classification registry to be used for getting a reference
    /// to the custom classification type later.
    /// </summary>
    [Import]
    internal IClassificationTypeRegistryService ClassificationRegistry = null; // Set via MEF

    [Import]
    internal IConfigurationFileProvider ConfigurationFileProvider = null; // Set via MEF

    [Import]
    internal IBuildOutputParser BuildOutputParser = null; // Set via MEF

    public IClassifier GetClassifier(ITextBuffer buffer) {
      Func<BuildOutputClassifier> creator = () => new BuildOutputClassifier(ClassificationRegistry, BuildOutputParser);
      return buffer.Properties.GetOrCreateSingletonProperty(creator);
    }
  }
}
