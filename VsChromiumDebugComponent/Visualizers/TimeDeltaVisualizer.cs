
using Microsoft.VisualStudio.Debugger.Evaluation;
using System;

namespace VsChromium.Visualizers {
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

          long microseconds = long.Parse(deltaResult.Value);
          TimeSpan span = TimeSpan.FromMilliseconds((double)microseconds / 1000.0);
          string formattedResult = span.ToString();

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
