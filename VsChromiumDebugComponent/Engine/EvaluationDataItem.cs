using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;
using System.Diagnostics;


namespace VsChromium
{
  public class EvaluationDataItem : DkmDataItem
  {
    private DkmVisualizedExpression expression_;
    private DkmEvaluationResult evalResult_;
    private BasicVisualizer visualizer_;

    public EvaluationDataItem(DkmVisualizedExpression expression, DkmEvaluationResult evalResult)
    {
      expression_ = expression;
      evalResult_ = evalResult;

      VisualizerRegistrar.TryCreateVisualizer(expression, out visualizer_);
    }

    protected override void OnClose()
    {
      base.OnClose();
      //evalResult_.Close();
    }

    public DkmVisualizedExpression Expression
    {
      get { return expression_; }
    }

    public DkmEvaluationResult EvaluationResult
    {
      get { return evalResult_; }
    }

    public BasicVisualizer Visualizer
    {
      get { return visualizer_; }
    }
  }
}
