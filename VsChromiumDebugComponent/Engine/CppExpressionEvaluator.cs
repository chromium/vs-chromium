using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger;

namespace VsChromium
{
  public static class CppExpressionEvaluator
  {
    public static DkmLanguage CppLanguage = DkmLanguage.Create("C++", new DkmCompilerId(Guids.MicrosoftVendorGuid, Guids.CppLanguageGuid));

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
