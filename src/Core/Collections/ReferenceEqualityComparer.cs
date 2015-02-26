// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// Implementation of IEqualityComparer where object references is the
  /// identity.
  /// </summary>
  public class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class {
    public static ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

    public bool Equals(T x, T y) {
      return object.ReferenceEquals(x, y);
    }

    public int GetHashCode(T obj) {
      return RuntimeHelpers.GetHashCode(obj);
    }
  }
}