using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger.Evaluation;

namespace VsChromium
{
  public interface IVisualizerFactory
  {
    BasicVisualizer CreateVisualizer(DkmVisualizedExpression expression);
  }
}
