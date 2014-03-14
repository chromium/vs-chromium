using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.Evaluation;


namespace ChromeVis
{
    public class DebugComponent : IDkmCustomVisualizer
    {
      static DebugComponent()
      {
        VisualizerRegistrar.Register<MyCustomTypeVisualizer.Factory>(Guids.ChromeNativeVisualizerId);
      }

      private bool TryGetRegisteredVisualizer(DkmVisualizedExpression expression, out BasicVisualizer visualizer, out DkmFailedEvaluationResult failureResult)
      {
        visualizer = null;
        failureResult = null;

        if (VisualizerRegistrar.TryCreateVisualizer(expression, out visualizer))
          return true;

        string name = null;
        string fullName = null;
        Utility.GetExpressionName(expression, out name, out fullName);

        DkmFailedEvaluationResult failure = DkmFailedEvaluationResult.Create(
            expression.InspectionContext,
            expression.StackFrame,
            name,
            fullName,
            String.Format("No formatter is registered for VisualizerId {0}",
                expression.VisualizerId),
            DkmEvaluationResultFlags.Invalid,
            null);
        failureResult = failure;
        return false;
      }

      void IDkmCustomVisualizer.EvaluateVisualizedExpression(DkmVisualizedExpression expression, out DkmEvaluationResult resultObject)
      {
        BasicVisualizer visualizer = null;
        DkmFailedEvaluationResult failureResult = null;

        if (!TryGetRegisteredVisualizer(expression, out visualizer, out failureResult))
        {
          resultObject = failureResult;
          return;
        }

        DkmEvaluationResult evalResult = visualizer.EvaluationResult;
        EvaluationDataItem resultDataItem = new EvaluationDataItem(expression, evalResult);

        expression.SetDataItem(DkmDataCreationDisposition.CreateAlways, resultDataItem);

        string name = null;
        string fullName = null;
        Utility.GetExpressionName(expression, out name, out fullName);

        if (evalResult.TagValue == DkmEvaluationResult.Tag.SuccessResult) 
        {
          DkmSuccessEvaluationResult successResult = (DkmSuccessEvaluationResult)evalResult;
          resultObject = DkmSuccessEvaluationResult.Create(
              successResult.InspectionContext,
              successResult.StackFrame,
              name,
              successResult.FullName,
              successResult.Flags,
              successResult.Value,
              successResult.EditableValue,
              successResult.Type,
              successResult.Category,
              successResult.Access,
              successResult.StorageType,
              successResult.TypeModifierFlags,
              successResult.Address,
              successResult.CustomUIVisualizers,
              successResult.ExternalModules,
              resultDataItem);
        }
        else
        {
          DkmFailedEvaluationResult failResult = (DkmFailedEvaluationResult)evalResult;

          resultObject = DkmFailedEvaluationResult.Create(
              failResult.InspectionContext,
              failResult.StackFrame,
              name,
              fullName,
              failResult.ErrorMessage,
              failResult.Flags,
              null);
          return;
        }
      }

      void IDkmCustomVisualizer.GetChildren(DkmVisualizedExpression expression, int initialRequestSize, DkmInspectionContext inspectionContext, out DkmChildVisualizedExpression[] initialChildren, out DkmEvaluationResultEnumContext enumContext)
      {
        EvaluationDataItem dataItem = expression.GetDataItem<EvaluationDataItem>();
        if (dataItem == null)
        {
          Debug.Fail("DebugComponent.GetChildren passed a visualized expression that does not have an associated ExpressionDataItem.");
          throw new NotSupportedException();
        }

        initialChildren = new DkmChildVisualizedExpression[0];

        enumContext = DkmEvaluationResultEnumContext.Create(
            dataItem.Visualizer.ChildElementCount,
            expression.StackFrame,
            expression.InspectionContext,
            null);
      }

      void IDkmCustomVisualizer.GetItems(DkmVisualizedExpression expression, DkmEvaluationResultEnumContext enumContext, int startIndex, int count, out DkmChildVisualizedExpression[] items)
      {
        EvaluationDataItem dataItem = expression.GetDataItem<EvaluationDataItem>();
        if (dataItem == null)
        {
          Debug.Fail("DebugComponent.GetItems passed a visualized expression that does not have an associated ExpressionDataItem.");
          throw new NotSupportedException();
        }

        items = dataItem.Visualizer.GetChildItems(startIndex, count);
      }

      string IDkmCustomVisualizer.GetUnderlyingString(DkmVisualizedExpression visualizedExpression)
      {
        throw new NotImplementedException();
      }

      void IDkmCustomVisualizer.SetValueAsString(DkmVisualizedExpression visualizedExpression, string value, int timeout, out string errorText)
      {
        throw new NotImplementedException();
      }

      void IDkmCustomVisualizer.UseDefaultEvaluationBehavior(DkmVisualizedExpression expression, out bool useDefaultEvaluationBehavior, out DkmEvaluationResult defaultEvaluationResult)
      {
        BasicVisualizer visualizer = null;

        defaultEvaluationResult = null;
        useDefaultEvaluationBehavior = true;
        if (expression.VisualizerId != Guids.ForceDefaultVisualizationGuid &&
            VisualizerRegistrar.TryCreateVisualizer(expression, out visualizer))
        {
          // If this visualizer has custom fields, or displays default fields non-inline, don't use
          // the default evaluation behavior.
          ChildDisplayFlags flags = visualizer.ChildDisplayFlags;
          if (flags.HasFlag(ChildDisplayFlags.HasCustomFields) ||
              !flags.HasFlag(ChildDisplayFlags.DefaultFieldsInline))
            useDefaultEvaluationBehavior = false;
        }

        if (useDefaultEvaluationBehavior)
        {
          string name = null;
          string fullName = null;
          Utility.GetExpressionName(expression, out name, out fullName);
          defaultEvaluationResult = CppExpressionEvaluator.Evaluate(expression, fullName);
        }
      }
    }
}
