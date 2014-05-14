// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.DkmIntegration.ServerComponent.FrameAnalyzers {
  // StackFrameAnalyzer is an abstract class which concrete implementations of will override to
  // to provide support for extracting rich parameter information from stack frames with known
  // calling conventions.
  public abstract class StackFrameAnalyzer {
    protected List<FunctionParameter> _parameters;

    public StackFrameAnalyzer(IEnumerable<FunctionParameter> parameters) {
      // Deep copy the FunctionParameter list.
      this._parameters = new List<FunctionParameter>();
      foreach (FunctionParameter param in parameters) {
        this._parameters.Add(new FunctionParameter(param.Name, param.Size, param.WordSize));
      }
    }

    public abstract ulong PrologueLength { get; }
    public abstract object GetArgumentValue(DkmStackWalkFrame frame, int index);

    public FunctionParameter[] Parameters {
      get { return _parameters.ToArray(); }
    }

    public object[] GetAllArgumentValues(DkmStackWalkFrame frame) {
      object[] arguments = new object[_parameters.Count];
      for (int i = 0; i < _parameters.Count; ++i)
        arguments[i] = GetArgumentValue(frame, i);
      return arguments;
    }

    public object GetArgumentValue(DkmStackWalkFrame frame, string name) {
      for (int i = 0; i < _parameters.Count; ++i) {
        if (_parameters[i].Name.Equals(name))
          return GetArgumentValue(frame, i);
      }
      return null;
    }
  }
}
