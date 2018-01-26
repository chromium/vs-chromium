// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Logging;

namespace VsChromium.DkmIntegration.ServerComponent {
  public enum ParameterType {
    Int32,
    Int64,
    Pointer
  }

  public class FunctionParameter {
    private string _name;
    private ParameterType _type;
    public FunctionParameter(string name, ParameterType type) {
      _name = name;
      _type = type;
    }

    public string Name { get { return _name; } }
    public ParameterType Type { get { return _type; } }

    public int GetSize(int wordSize) {
      Invariants.Assert((wordSize & (wordSize - 1)) == 0);

      switch (_type) {
        case ParameterType.Int32: 
          return 4;
        case ParameterType.Int64: 
          return 8;
        default:
          Invariants.Assert(_type == ParameterType.Pointer);
          return wordSize;
      }
    }

    public int GetPaddedSize(int wordSize) {
      Invariants.Assert((wordSize & (wordSize - 1)) == 0);

      return (GetSize(wordSize) + (wordSize - 1)) & ~(wordSize - 1);
    }
  }
}
