using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace VsChromium.Visualizers
{
  public class MyCustomTypeVisualizer : BasicVisualizer
  {
    public class Factory : IVisualizerFactory
    {
      public BasicVisualizer CreateVisualizer(DkmVisualizedExpression expression)
      {
        return new MyCustomTypeVisualizer(expression);
      }
    }

    private MyCustomTypeVisualizer(DkmVisualizedExpression expression)
      : base(expression)
    {
      RegisterCustomChildEntry("MyX", HandleEvaluateChild);
      RegisterCustomChildEntry("MyY", HandleEvaluateChild);
      RegisterCustomChildEntry("MyZ", HandleEvaluateChild);
      RegisterDefaultChildEntries(ChildDisplayMode.Inline);
    }

    private DkmEvaluationResult HandleEvaluateChild(string name, int index)
    {
      EvaluationDataItem originalDataItem = expression_.GetDataItem<EvaluationDataItem>();

      DkmSuccessEvaluationResult result = DkmSuccessEvaluationResult.Create(
          expression_.InspectionContext,
          expression_.StackFrame,
          name,
          name,
          DkmEvaluationResultFlags.ReadOnly,
          name + " value",
          null,
          null,
          DkmEvaluationResultCategory.Other,
          DkmEvaluationResultAccessType.None,
          DkmEvaluationResultStorageType.None,
          DkmEvaluationResultTypeModifierFlags.None,
          null,
          null,
          null,
          originalDataItem);

      return result;
    }

    public override DkmEvaluationResult EvaluationResult
    {
      get
      {
        if (expression_.TagValue == DkmVisualizedExpression.Tag.RootVisualizedExpression)
        {
          DkmRootVisualizedExpression rootExpr = (DkmRootVisualizedExpression)expression_;

          DkmSuccessEvaluationResult xresult = CppExpressionEvaluator.EvaluateSuccess(rootExpr, rootExpr.FullName + ".x,!");
          DkmSuccessEvaluationResult yresult = CppExpressionEvaluator.EvaluateSuccess(rootExpr, rootExpr.FullName + ".y,!");
          DkmSuccessEvaluationResult zresult = CppExpressionEvaluator.EvaluateSuccess(rootExpr, rootExpr.FullName + ".z,!");
          DkmSuccessEvaluationResult rootexpr = CppExpressionEvaluator.EvaluateSuccess(rootExpr, rootExpr.FullName + ",!");

          float f = float.Parse(zresult.Value);
          string formattedResult = string.Format("{{ x = {0}, y = {1}, z = {2:0.00} }}", xresult.Value, yresult.Value, f);

          DkmEvaluationResult result = DkmSuccessEvaluationResult.Create(
              rootExpr.InspectionContext,
              rootExpr.StackFrame,
              rootExpr.Name,
              rootexpr.FullName,
              DkmEvaluationResultFlags.Expandable | DkmEvaluationResultFlags.ReadOnly,
              formattedResult,
              null,
              rootexpr.Type,
              rootexpr.Category,
              rootexpr.Access,
              rootexpr.StorageType,
              rootexpr.TypeModifierFlags,
              rootexpr.Address,
              rootexpr.CustomUIVisualizers,
              rootexpr.ExternalModules,
              null);
          return result;
        }
        else
          return null;
      }
    }

    public override ChildDisplayFlags ChildDisplayFlags
    {
      get { return ChildDisplayFlags.DefaultFieldsInline | ChildDisplayFlags.HasCustomFields | ChildDisplayFlags.HasDefaultFields; }
    }
  }
}
