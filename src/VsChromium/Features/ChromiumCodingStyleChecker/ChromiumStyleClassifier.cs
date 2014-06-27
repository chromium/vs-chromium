// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using VsChromium.Core.Configuration;
using VsChromium.Core.FileNames;

namespace VsChromium.Features.ChromiumCodingStyleChecker {
  /// <summary>
  /// Classifier that classifies all text as an instance of the OrinaryClassifierType
  /// </summary>
  public class ChromiumStyleClassifier : IClassifier {
    private readonly IEnumerable<ITextLineChecker> _checkers;
    private readonly IConfigurationFileProvider _configurationFileProvider;
    private readonly IClassificationType _classificationType;
    private readonly Lazy<IList<string>> _disabledCheckers;

    internal ChromiumStyleClassifier(
      IClassificationTypeRegistryService classificationRegistry,
      IEnumerable<ITextLineChecker> checkers,
      IConfigurationFileProvider configurationFileProvider) {
      _classificationType = classificationRegistry.GetClassificationType(ChromiumStyleClassifierConstants.Name);
      _checkers = checkers;
      _configurationFileProvider = configurationFileProvider;
      _disabledCheckers = new Lazy<IList<string>>(ReadDisableCheckers);
    }

    private IList<string> ReadDisableCheckers() {
      return
        _configurationFileProvider.ReadFile(new RelativePathName(ConfigurationStyleFilenames.ChromiumStyleCheckersDisabled),
                                            (filename, x) => x.Where(line => !line.TrimStart().StartsWith("#"))).ToList();
    }

    /// <summary>
    /// This method scans the given SnapshotSpan for potential matches for this classification.
    /// In this instance, it classifies everything and returns each span as a new ClassificationSpan.
    /// </summary>
    /// <param name="span">The span currently being classified</param>
    /// <returns>A list of ClassificationSpans that represent spans identified to be of this classification</returns>
    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
      var checkers = _checkers
        .Where(checker => !_disabledCheckers.Value.Contains(checker.GetType().Name))
        .Where(checker => checker.AppliesToContentType(span.Snapshot.ContentType));

      //create a list to hold the results
      var startLine = span.Start.GetContainingLine().LineNumber;
      var endLine = (span.End > span.Start)
                      ? (span.End - 1).GetContainingLine().LineNumber
                      : span.End.GetContainingLine().LineNumber;
      var classifications = Enumerable
        .Range(startLine, endLine - startLine + 1)
        .Select(lineNum => span.Snapshot.GetLineFromLineNumber(lineNum))
        .SelectMany(line => checkers.Select(checker => checker.CheckLine(line)))
        .SelectMany(errors => errors)
        .Select(error => CreateClassification(error))
        .ToList();
      return classifications;
    }

    private ClassificationSpan CreateClassification(TextLineCheckerError error) {
      return new ClassificationSpan(error.Span, _classificationType);
    }

#pragma warning disable 67
    // This event gets raised if a non-text change would affect the classification in some way,
    // for example typing /* would cause the classification to change in C# without directly
    // affecting the span.
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67
  }
}
