// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.DkmIntegration.IdeComponent;

namespace VsChromium.DkmIntegration.Visualizers {
  class DateTimeVisualizer : BasicVisualizer {
    public class Factory : IVisualizerFactory {
      public BasicVisualizer CreateVisualizer(DkmVisualizedExpression expression) {
        return new DateTimeVisualizer(expression);
      }
    }

    public DateTimeVisualizer(DkmVisualizedExpression expression)
        : base(expression) {
      RegisterDefaultChildEntries(ChildDisplayMode.Inline);
    }

    public override DkmEvaluationResult EvaluationResult {
      get {
        if (expression_.TagValue == DkmVisualizedExpression.Tag.RootVisualizedExpression) {
          DkmRootVisualizedExpression rootExpr = (DkmRootVisualizedExpression)expression_;
          DkmSuccessEvaluationResult usResult = CppExpressionEvaluator.EvaluateSuccess(rootExpr, rootExpr.FullName + ".us_,!");
          DkmSuccessEvaluationResult rootExprResult = CppExpressionEvaluator.EvaluateSuccess(rootExpr, rootExpr.FullName + ",!");

          long microseconds;
          string formattedResult = "{ us_ = " + usResult.Value + " }";
          if (long.TryParse(usResult.Value, out microseconds)) {
            long fileTime = microseconds * 10;
            try {
              DateTime dt = DateTime.FromFileTime(fileTime);
              formattedResult = dt.ToString();
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
