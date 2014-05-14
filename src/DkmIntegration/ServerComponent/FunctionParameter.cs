// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.DkmIntegration.ServerComponent {
  public class FunctionParameter {
    private string _name;
    private int _size;
    private int _wordSize;
    public FunctionParameter(string name, int size, int wordSize) {
      // Ensure that wordSize is a power of 2.
      Debug.Assert((wordSize & (wordSize - 1)) == 0);

      this._name = name;
      this._size = size;
      this._wordSize = wordSize;
    }
    public string Name { get { return _name; } }
    public int Size { get { return _size; } }
    public int WordSize { get { return _wordSize; } }
    public int PaddedSize { 
      get {
        // When n is a power of 2, (x + n - 1) & ~(n - 1) rounds x up to the next multiple of n.
        return (Size + (WordSize - 1)) & ~(WordSize - 1);
      }
    }
  }
}
