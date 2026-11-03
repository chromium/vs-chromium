// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;

namespace VsChromium.Tests.VsChromium.ServerProxy {
  [TestClass]
  public class TestServerError {
    [TestMethod]
    public void RecoverableErrorCodeIsRecoverable() {
      var error = ServerError.FromException(new RemoteInvocationException("Invalid regular expression", -32001, (object)null));
      Assert.AreEqual(ServerErrorKind.Recoverable, error.Kind);
      Assert.AreEqual("Invalid regular expression", error.GetBaseError().Message);
      Assert.IsTrue(ServerError.IsReportableError(error));
    }

    [TestMethod]
    public void CancelledErrorCodeIsCancelled() {
      var error = ServerError.FromException(new RemoteInvocationException("Request cancelled", -32800, (object)null));
      Assert.IsTrue(error.IsOperationCanceled);
      Assert.IsFalse(ServerError.IsReportableError(error));

      error = ServerError.FromException(new AggregateException(new OperationCanceledException()));
      Assert.IsTrue(error.IsOperationCanceled);
    }

    [TestMethod]
    public void InternalErrorDataIsDeserialized() {
      var data = JToken.FromObject(new ErrorInfo {
        Message = "Outer",
        TypeName = "System.InvalidOperationException",
        StackTrace = "at Foo()",
        Inner = new ErrorInfo { Message = "Inner", TypeName = "System.IO.IOException" },
      });
      var error = ServerError.FromException(new RemoteInvocationException("Outer", -32603, data));
      Assert.AreEqual(ServerErrorKind.Unexpected, error.Kind);
      Assert.AreEqual("Outer", error.Message);
      Assert.AreEqual("System.InvalidOperationException", error.TypeName);
      Assert.AreEqual("at Foo()", error.StackTrace);
      Assert.IsNotNull(error.InnerError);
      Assert.AreEqual("Inner", error.InnerError.Message);
      Assert.AreEqual("System.IO.IOException", error.InnerError.TypeName);
    }

    [TestMethod]
    public void LocalExceptionIsUnexpected() {
      var error = ServerError.FromException(new IOException("Pipe broken", new InvalidOperationException("Inner")));
      Assert.AreEqual(ServerErrorKind.Unexpected, error.Kind);
      Assert.AreEqual("Pipe broken", error.Message);
      Assert.AreEqual("Inner", error.InnerError.Message);
    }

    [TestMethod]
    public void NotificationErrorKindIsDeterminedByTheBaseError() {
      Assert.IsNull(ServerError.FromErrorInfo(null));

      var recoverable = ServerError.FromErrorInfo(new ErrorInfo {
        Message = "Scan failed",
        TypeName = "System.Exception",
        Inner = new ErrorInfo { Message = "Invalid project path", TypeName = "VsChromium.Core.Ipc.RecoverableErrorException" },
      });
      Assert.IsTrue(recoverable.IsRecoverable);
      Assert.AreEqual("Invalid project path", recoverable.GetBaseError().Message);

      var unexpected = ServerError.FromErrorInfo(new ErrorInfo { Message = "Boom", TypeName = "System.Exception" });
      Assert.AreEqual(ServerErrorKind.Unexpected, unexpected.Kind);
      Assert.IsTrue(ServerError.IsReportableError(unexpected));
    }
  }
}
