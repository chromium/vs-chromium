// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace VsChromiumPackage.Features.BuildErrors {
  /// <summary>
  /// Classifier that classifies all text as an instance of the OrinaryClassifierType
  /// </summary>
  public class BuildOutputClassifier : IClassifier {
    private readonly IBuildOutputParser _buildOutputParser;
    private readonly IClassificationType _classificationType;

    internal BuildOutputClassifier(IClassificationTypeRegistryService classificationRegistry, IBuildOutputParser buildOutputParser) {
      _buildOutputParser = buildOutputParser;
      _classificationType = classificationRegistry.GetClassificationType("VsChromiumPackageBuildOutput");
    }

    /// <summary>
    /// This method scans the given SnapshotSpan for potential matches for this classification.
    /// In this instance, it classifies everything and returns each span as a new ClassificationSpan.
    /// </summary>
    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
      //create a list to hold the results
      var startLine = span.Start.GetContainingLine().LineNumber;
      var endLine = (span.End > span.Start)
                      ? (span.End - 1).GetContainingLine().LineNumber
                      : span.End.GetContainingLine().LineNumber;
      var classifications = Enumerable
        .Range(startLine, endLine - startLine + 1)
        .Select(lineNum => span.Snapshot.GetLineFromLineNumber(lineNum))
        .Select(line => new { TextSnapshotLine = line, BuildOutputSpan = ParseBuildOutput(line) })
        .Where(x => x.BuildOutputSpan != null)
        .Select(x => CreateClassification(x.TextSnapshotLine, x.BuildOutputSpan))
        .ToList();
      return classifications;
    }

    private BuildOutputSpan ParseBuildOutput(ITextSnapshotLine line) {
      return _buildOutputParser.ParseLine(line.GetText());
    }

    private ClassificationSpan CreateClassification(ITextSnapshotLine line, BuildOutputSpan buildOutputSpan) {
      var start = line.Start + buildOutputSpan.Index;
      var span = new SnapshotSpan(start, buildOutputSpan.Length);
      return new ClassificationSpan(span, _classificationType);
    }

#pragma warning disable 67
    // This event gets raised if a non-text change would affect the classification in some way,
    // for example typing /* would cause the classification to change in C# without directly
    // affecting the span.
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67
  }
}
