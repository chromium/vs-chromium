// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger.Evaluation;

namespace VsChromium.DkmIntegration.IdeComponent
{
  public static class VisualizerRegistrar
  {
    private static Dictionary<Guid, IVisualizerFactory> visualizers_;

    static VisualizerRegistrar()
    {
      visualizers_ = new Dictionary<Guid, IVisualizerFactory>();
    }

    public static void Register<Factory>(Guid guid) where Factory : IVisualizerFactory, new()
    {
      visualizers_[guid] = new Factory();
    }

    public static bool TryCreateVisualizer(DkmVisualizedExpression expression, out BasicVisualizer visualizer)
    {
      visualizer = null;

      IVisualizerFactory factory = null;
      bool result = visualizers_.TryGetValue(expression.VisualizerId, out factory);
      if (!result)
        return result;

      visualizer = factory.CreateVisualizer(expression);
      return true;
    }
  }
}
