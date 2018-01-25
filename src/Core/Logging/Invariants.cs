// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;

namespace VsChromium.Core.Logging {
  public static class Invariants {

    public static void CheckArgumentNotNull<T>(T value, string paramName, string message) where T : class {
      if (value == null) {
        ThrowArgumentNullException(paramName, message);
      }
    }

    public static void CheckArgumentNotNull<T>(T value, string paramName) where T : class {
      if (value == null) {
        ThrowArgumentNullException(paramName);
      }
    }

    public static void CheckArgument(bool condition, string paramName, string message) {
      if (!condition) {
        ThrowArgumentException(paramName, message);
      }
    }

    public static void CheckArgument(bool condition, string paramName, string format, object arg) {
      if (!condition) {
        ThrowArgumentException(paramName, string.Format(format, arg));
      }
    }

    public static void Assert(bool condition) {
      Assert(condition, "Assertion failed");
    }

    public static void Assert(bool condition, string message) {
      if (!condition) {
        LogAssertionFailed(message);
      }
      Debug.Assert(condition, message);
    }

    private static void ThrowArgumentNullException(string paramName, string message) {
      try {
        throw new ArgumentNullException(paramName, message);
      } catch (Exception e) {
        Logger.LogError(e, "Argument null: {0}", e.Message);
        throw;
      }
    }

    private static void ThrowArgumentNullException(string paramName) {
      try {
        throw new ArgumentNullException(paramName);
      } catch (Exception e) {
        Logger.LogError(e, "Argument null: {0}", e.Message);
        throw;
      }
    }

    private static void ThrowArgumentException(string paramName, string message) {
      try {
        throw new ArgumentException(paramName, message);
      } catch (Exception e) {
        Logger.LogError(e, "Argument: {0}", e.Message);
        throw;
      }
    }

    private static void LogAssertionFailed(string message) {
      try {
        throw new AssertionFailedException(message);
      } catch (Exception e) {
        Logger.LogError(e, e.Message);
      }
    }

    public class AssertionFailedException : Exception {
      public AssertionFailedException(string message) : base(message) {
      }
    }
  }
}
