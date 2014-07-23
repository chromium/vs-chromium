// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace VsChromium.DkmIntegration.IdeComponent
{
  public static class CppExpressionEvaluator
  {
    public static DkmLanguage CppLanguage = DkmLanguage.Create("C++", new DkmCompilerId(Guids.Vendor.Microsoft, Guids.Language.Cpp));

    // Evaluates the given expression in the specified context, and returns a successful result, throwing an exception
    // if evaluation failed.
    public static DkmSuccessEvaluationResult EvaluateSuccess(DkmVisualizedExpression expr, string text)
    {
      return EvaluateSuccess(expr, DkmEvaluationFlags.None, text, null);
    }

    public static DkmSuccessEvaluationResult EvaluateSuccess(DkmVisualizedExpression expr, DkmEvaluationFlags flags, string text, DkmDataItem data)
    {
      DkmEvaluationResult result = Evaluate(expr, flags, text, data);
      if (result.TagValue != DkmEvaluationResult.Tag.SuccessResult)
        throw new EvaluationException(result);

      return (DkmSuccessEvaluationResult)result;
    }

    // Evaluates the given expression in the specified context and returns the result.
    public static DkmEvaluationResult Evaluate(DkmVisualizedExpression expr, string text)
    {
      return Evaluate(expr, DkmEvaluationFlags.None, text, null);
    }

    public static DkmEvaluationResult Evaluate(DkmVisualizedExpression expr, DkmEvaluationFlags flags, string text, DkmDataItem data)
    {
      using (DkmLanguageExpression vexpr = DkmLanguageExpression.Create(CppLanguage, flags, text, data))
      {
        DkmEvaluationResult result = null;
        expr.EvaluateExpressionCallback(expr.InspectionContext, vexpr, expr.StackFrame, out result);

        return result;
      }
    }
  }
}
