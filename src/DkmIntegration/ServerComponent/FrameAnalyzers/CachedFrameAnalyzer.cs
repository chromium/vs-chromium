// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger.CallStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.DkmIntegration.ServerComponent.FrameAnalyzers {
  // Caches the parameter values from a stack frame so that they can be used after the frame is
  // destroyed.
  public class CachedFrameAnalyzer : StackFrameAnalyzer {
    object[] _paramValues;

    public CachedFrameAnalyzer(IEnumerable<FunctionParameter> parameters, object[] values)
        : base(parameters) {
      this._paramValues = values;
    }

    public override object GetArgumentValue(DkmStackWalkFrame frame, int index) {
      return _paramValues[index];
    }
  }
}
