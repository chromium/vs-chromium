// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using Microsoft.VisualStudio.Debugger.CallStack;

namespace VsChromium.DkmIntegration.ServerComponent.FrameAnalyzers {
  // Caches the parameter values from a stack frame so that they can be used after the frame is
  // destroyed.
  public class CachedFrameAnalyzer : StackFrameAnalyzer {
    object[] _paramValues;
    int _wordSize;

    public CachedFrameAnalyzer(IEnumerable<FunctionParameter> parameters, object[] values, int wordSize)
        : base(parameters) {
      this._paramValues = values;
      this._wordSize = wordSize;
    }

    public override object GetArgumentValue(DkmStackWalkFrame frame, int index) {
      return _paramValues[index];
    }

    public override int WordSize {
      get { return _wordSize; }
    }
  }
}
