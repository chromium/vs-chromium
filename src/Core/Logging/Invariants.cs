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

    public static void CheckOperation(bool condition, string message) {
      if (!condition) {
        ThrowInvalidOperationException(message);
      }
    }

    public static void Assert(bool condition) {
      Assert(condition, "Assertion failed");
    }

    public static void Assert(bool condition, string message) {
      if (!condition) {
        Fail(message);
      }
    }

    public static Exception Fail(string message) {
      LogAssertionFailed(Environment.StackTrace, message);
      Debug.Assert(false, message);
      throw new AssertionFailedException(message);
    }

    private static void ThrowArgumentNullException(string paramName, string message) {
      try {
        throw new ArgumentNullException(paramName, message);
      } catch (Exception e) {
        LogFailure(e, Environment.StackTrace, e.Message);
        throw;
      }
    }

    private static void ThrowArgumentNullException(string paramName) {
      try {
        throw new ArgumentNullException(paramName);
      } catch (Exception e) {
        LogFailure(e, Environment.StackTrace, e.Message);
        throw;
      }
    }

    private static void ThrowInvalidOperationException(string message) {
      try {
        throw new InvalidOperationException(message);
      } catch (Exception e) {
        LogFailure(e, Environment.StackTrace, e.Message);
        throw;
      }
    }

    private static void ThrowArgumentException(string paramName, string message) {
      try {
        throw new ArgumentException(paramName, message);
      } catch (Exception e) {
        LogFailure(e, Environment.StackTrace, e.Message);
        throw;
      }
    }

    private static void LogFailure(Exception error, string stackTrace, string message) {
      var e = new StackTraceException(message, stackTrace, error);
      Logger.LogError(e, message);
    }

    private static void LogAssertionFailed(string stackTrace, string message) {
      var e = new AssertionFailedException(message, stackTrace);
      Logger.LogError(e, message);
    }
  }
}
