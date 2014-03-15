using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger;

namespace VsChromium
{
  // Summary:
  //     Interface for all child evaluators.  Implement this interface to provide custom
  //     evaluation of child elements.
  public interface IChildEvaluator
  {
    void EvaluateChildren(DkmChildVisualizedExpression[] output, int startIndex, out int numWritten);
    int ChildCount { get; }
  }

  public delegate DkmEvaluationResult CustomEvaluationHandler(string name, int index);

  // Summary:
  //     IChildEvaluator implementation which delegates evaluation to the default evaluation
  //     handler.  This implementation provides the ability to inline the children directly
  //     as siblings of other child items, or to nest them under a parent node with the
  //     name [Default View].
  public class DefaultChildEvaluator : IChildEvaluator
  {
    private ChildDisplayMode mode_;
    private DkmVisualizedExpression expression_;
    private DkmEvaluationResultEnumContext defEnumContext_;

    public DefaultChildEvaluator(DkmVisualizedExpression expression, ChildDisplayMode mode)
    {
      this.mode_ = mode;
      this.expression_ = expression;
      this.defEnumContext_ = null;
    }

    private void CreateDefaultEnumContext()
    {
      if (defEnumContext_ != null)
        return;

      string name = null;
      string fullName = null;
      Utility.GetExpressionName(expression_, out name, out fullName);
      DkmEvaluationResult defaultEvaluationResult = CppExpressionEvaluator.Evaluate(expression_, fullName + ",!");

      DkmEvaluationResult[] defInitialChildren;
      expression_.GetChildrenCallback(defaultEvaluationResult, 0, expression_.InspectionContext, out defInitialChildren, out defEnumContext_);

      foreach (DkmEvaluationResult evalResult in defInitialChildren)
        evalResult.Close();
    }

    public void EvaluateChildren(DkmChildVisualizedExpression[] output, int startIndex, out int numWritten)
    {
      numWritten = 0;
      if (mode_ == ChildDisplayMode.Inline)
      {
        CreateDefaultEnumContext();

        int count = ChildCount;
        DkmEvaluationResult[] results = new DkmEvaluationResult[count];
        expression_.GetItemsCallback(defEnumContext_, 0, count, out results);
        for (int i = 0; i < count; ++i)
        {
          DkmSuccessEvaluationResult successResult = results[i] as DkmSuccessEvaluationResult;
          DkmExpressionValueHome home = null;
          if (successResult != null && successResult.Address != null) {
              home = DkmPointerValueHome.Create(successResult.Address.Value);
          } else {
              home = DkmFakeValueHome.Create(0);
          }

          output[startIndex+i] = DkmChildVisualizedExpression.Create(
              defEnumContext_.InspectionContext,
              Guids.ForceDefaultVisualizationGuid,
              expression_.SourceId,
              defEnumContext_.StackFrame,
              home,
              results[i],
              expression_,
              (uint)startIndex,
              null);
          EvaluationDataItem originalDataItem = results[i].GetDataItem<EvaluationDataItem>();
        }
        numWritten = count;
      }
      else
        numWritten = 1;
    }

    public int ChildCount
    {
      get
      {
        if (mode_ == ChildDisplayMode.Inline)
        {
          CreateDefaultEnumContext();

          return defEnumContext_.Count;
        }
        else
          return 1;
      }
    }
  }

  // Summary:
  //     IChildEvaluator implementation which injects a custom child entry with the specified
  //     name into the watch window.  The value displayed for this entry is obtained by calling
  //     back to the specified evaluation delegate.
  public class CustomChildEvaluator : IChildEvaluator
  {
    string name_;
    CustomEvaluationHandler evaluator_;
    DkmVisualizedExpression expression_;

    public CustomChildEvaluator(DkmVisualizedExpression expression, string name, CustomEvaluationHandler evaluator)
    {
      this.name_ = name;
      this.evaluator_ = evaluator;
      this.expression_ = expression;
    }

    public void EvaluateChildren(DkmChildVisualizedExpression[] output, int startIndex, out int numWritten)
    {
      DkmEvaluationResult evalResult = evaluator_(name_, startIndex);
      EvaluationDataItem originalDataItem = evalResult.GetDataItem<EvaluationDataItem>();
      DkmChildVisualizedExpression childExpr = DkmChildVisualizedExpression.Create(
          expression_.InspectionContext,
          expression_.VisualizerId,
          expression_.SourceId,
          expression_.StackFrame,
          DkmFakeValueHome.Create(0),
          evalResult,
          expression_,
          (uint)startIndex,
          originalDataItem);

      output[startIndex] = childExpr;
      numWritten = 1;
    }

    public int ChildCount
    {
      get { return 1; }
    }
  }
}
