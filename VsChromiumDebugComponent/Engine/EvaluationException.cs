using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger.Evaluation;

namespace ChromeVis
{
  class EvaluationException : Exception
  {
    DkmEvaluationResult result_ = null;

    public EvaluationException(DkmEvaluationResult result)
    {
      result_ = result;
    }
  }
}
