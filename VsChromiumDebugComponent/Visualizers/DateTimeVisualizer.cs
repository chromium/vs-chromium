using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger.Evaluation;

namespace VsChromium.Visualizers {
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

          long microseconds = long.Parse(usResult.Value);
          long fileTime = microseconds * 10;
          DateTime dt = DateTime.FromFileTime(fileTime);
          string formattedResult = dt.ToString();

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
