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
  public interface IVisualizerFactory
  {
    BasicVisualizer CreateVisualizer(DkmVisualizedExpression expression);
  }
}
