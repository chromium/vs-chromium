// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;

namespace VsChromium.Core.Utility {
  public static class ExceptionExtensions {
    public static bool IsCanceled(this Exception e) {
      if (e.GetBaseException() is OperationCanceledException) {
          return true;
      }
      var aggregateException = e as AggregateException;
      if (aggregateException != null) {
        return aggregateException.InnerExceptions.Any(IsCanceled);
      }
      return false;
    }
  }
}
