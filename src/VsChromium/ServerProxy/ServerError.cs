// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using VsChromium.Protocol.Generated;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// The kind of a <see cref="ServerError"/>.
  /// </summary>
  public enum ServerErrorKind {
    /// <summary>
    /// Unexpected error (internal error of the server, connection lost, server
    /// not started, ...).
    /// </summary>
    Unexpected,
    /// <summary>
    /// Expected error, with a message to show to the user as is (e.g. invalid
    /// regular expression): JSON-RPC error code -32001.
    /// </summary>
    Recoverable,
    /// <summary>
    /// The request was cancelled (by the client, or by the server when a new
    /// search superseded the request): JSON-RPC error code -32800.
    /// </summary>
    Cancelled,
  }

  /// <summary>
  /// Description of an error of a request sent to the server (or of a server
  /// operation reported in a notification), for display and logging.
  /// </summary>
  public class ServerError {
    /// <summary>JSON-RPC error code of an expected error (message to show to the user).</summary>
    public const int RecoverableErrorCode = -32001;
    /// <summary>JSON-RPC error code of a cancelled request.</summary>
    public const int RequestCancelledErrorCode = -32800;

    public ServerErrorKind Kind { get; set; }
    public string Message { get; set; }
    /// <summary>Full name of the .NET exception type, if known.</summary>
    public string TypeName { get; set; }
    public string StackTrace { get; set; }
    public ServerError InnerError { get; set; }

    public bool IsRecoverable => Kind == ServerErrorKind.Recoverable;
    public bool IsOperationCanceled => Kind == ServerErrorKind.Cancelled;

    /// <summary>
    /// True if the error should be reported to the user (i.e. it is not a
    /// cancellation).
    /// </summary>
    public static bool IsReportableError(ServerError error) {
      return error != null && !error.IsOperationCanceled;
    }

    public Exception CreateException() {
      return new ServerErrorException(this);
    }

    /// <summary>
    /// The deepest inner error (this error if there is no inner error).
    /// </summary>
    public ServerError GetBaseError() {
      var result = this;
      while (result.InnerError != null)
        result = result.InnerError;
      return result;
    }

    /// <summary>
    /// Creates the description of an error reported by the server in a
    /// notification (e.g. a failed file system scan). Returns null if
    /// <paramref name="info"/> is null. The kind of the error is determined by
    /// the type of the deepest exception.
    /// </summary>
    public static ServerError FromErrorInfo(ErrorInfo info) {
      var result = FromErrorInfoWorker(info);
      if (result != null) {
        var typeName = result.GetBaseError().TypeName ?? "";
        if (typeName.EndsWith(".RecoverableErrorException", StringComparison.Ordinal)) {
          result.Kind = ServerErrorKind.Recoverable;
        } else if (typeName == typeof(OperationCanceledException).FullName ||
                   typeName == typeof(TaskCanceledException).FullName) {
          result.Kind = ServerErrorKind.Cancelled;
        }
      }
      return result;
    }

    private static ServerError FromErrorInfoWorker(ErrorInfo info) {
      if (info == null)
        return null;
      return new ServerError {
        Kind = ServerErrorKind.Unexpected,
        Message = info.Message,
        TypeName = info.TypeName,
        StackTrace = info.StackTrace,
        InnerError = FromErrorInfoWorker(info.Inner),
      };
    }

    /// <summary>
    /// Creates the description of the error of a failed request.
    /// </summary>
    public static ServerError FromException(Exception e) {
      if (e == null)
        return null;

      var aggregate = e as AggregateException;
      if (aggregate != null && aggregate.InnerExceptions.Count == 1) {
        return FromException(aggregate.InnerExceptions[0]);
      }

      if (e is OperationCanceledException) {
        return new ServerError {
          Kind = ServerErrorKind.Cancelled,
          Message = e.Message,
          TypeName = e.GetType().FullName,
        };
      }

      var remoteError = e as RemoteInvocationException;
      if (remoteError != null) {
        var info = GetErrorInfo(remoteError);
        return new ServerError {
          Kind = remoteError.ErrorCode == RecoverableErrorCode
            ? ServerErrorKind.Recoverable
            : remoteError.ErrorCode == RequestCancelledErrorCode
              ? ServerErrorKind.Cancelled
              : ServerErrorKind.Unexpected,
          Message = remoteError.Message,
          TypeName = info?.TypeName ?? remoteError.GetType().FullName,
          StackTrace = info?.StackTrace,
          InnerError = FromErrorInfoWorker(info?.Inner),
        };
      }

      return new ServerError {
        Kind = ServerErrorKind.Unexpected,
        Message = e.Message,
        TypeName = e.GetType().FullName,
        StackTrace = e.StackTrace,
        InnerError = FromException(e.InnerException),
      };
    }

    /// <summary>
    /// The "data" of an internal error (-32603) is an <see cref="ErrorInfo"/>
    /// (with the type name and stack trace of the server exception).
    /// </summary>
    private static ErrorInfo GetErrorInfo(RemoteInvocationException e) {
      try {
        var token = e.ErrorData as JToken;
        if (token != null && token.Type == JTokenType.Object) {
          return token.ToObject<ErrorInfo>();
        }
        return e.DeserializedErrorData as ErrorInfo;
      }
      catch (Exception) {
        return null;
      }
    }
  }

  /// <summary>
  /// Exception wrapping a <see cref="ServerError"/>.
  /// </summary>
  public class ServerErrorException : Exception {
    public ServerErrorException(ServerError error)
      : base(error.Message) {
      Error = error;
    }

    public ServerError Error { get; }
  }

  public static class ServerTaskExtensions {
    /// <summary>
    /// Converts a task without result (e.g. a request returning "void") to a
    /// task with a null result, for APIs expecting a result.
    /// </summary>
    public static async Task<object> WithNullResultAsync(this Task task) {
      // The task is a request of the JSON-RPC connection: it completes on the
      // thread pool, independently of the caller context.
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
      await task.ConfigureAwait(false);
#pragma warning restore VSTHRD003
      return null;
    }
  }
}
