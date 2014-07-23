// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.Debugger.Evaluation;
using VsChromium.DkmIntegration.IdeComponent;

namespace VsChromium.DkmIntegration.Visualizers {
  class TimeDeltaVisualizer : BasicVisualizer {
    public class Factory : IVisualizerFactory {
      public BasicVisualizer CreateVisualizer(DkmVisualizedExpression expression) {
        return new TimeDeltaVisualizer(expression);
      }
    }

    public TimeDeltaVisualizer(DkmVisualizedExpression expression)
      : base(expression) {
      RegisterDefaultChildEntries(ChildDisplayMode.Inline);
    }

    public override DkmEvaluationResult EvaluationResult {
      get {
        if (expression_.TagValue == DkmVisualizedExpression.Tag.RootVisualizedExpression) {
          DkmRootVisualizedExpression rootExpr = (DkmRootVisualizedExpression)expression_;
          DkmSuccessEvaluationResult deltaResult = CppExpressionEvaluator.EvaluateSuccess(rootExpr, rootExpr.FullName + ".delta_,!");
          DkmSuccessEvaluationResult rootExprResult = CppExpressionEvaluator.EvaluateSuccess(rootExpr, rootExpr.FullName + ",!");

          long microseconds;
          string formattedResult = "{ delta_ = " + deltaResult.Value + " }";
          if (long.TryParse(deltaResult.Value, out microseconds)) {
            try {
              TimeSpan span = TimeSpan.FromMilliseconds((double)microseconds / 1000.0);
              formattedResult = span.ToString();
            } catch {
              // Empty.
            }
          }

          DkmEvaluationResult result = DkmSuccessEvaluationResult.Create(
              rootExpr.InspectionContext,
              rootExpr.StackFrame,
              rootExpr.Name,
              rootExpr.FullName,
              DkmEvaluationResultFlags.Expandable | DkmEvaluationResultFlags.ReadOnly,
              formattedResult,
              null,
              rootExprResult.Type,
              rootExprResult.Category,
              rootExprResult.Access,
              rootExprResult.StorageType,
              rootExprResult.TypeModifierFlags,
              rootExprResult.Address,
              rootExprResult.CustomUIVisualizers,
              rootExprResult.ExternalModules,
              null);
          return result;
        } else
          return null;
      }
    }

    public override ChildDisplayFlags ChildDisplayFlags {
      get { return ChildDisplayFlags.DefaultFieldsInline | ChildDisplayFlags.HasDefaultFields; }
    }
  }
}
