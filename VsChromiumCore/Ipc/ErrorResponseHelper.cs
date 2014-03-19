using System;

namespace VsChromium.Core.Ipc {
  public static class ErrorResponseHelper {
    public static IpcResponse CreateIpcErrorResponse(IpcRequest request, Exception error) {
      return new IpcResponse {
        RequestId = request.RequestId,
        Protocol = IpcProtocols.Exception,
        Data = CreateErrorResponse(error)
      };
    }

    public static ErrorResponse CreateErrorResponse(Exception e) {
      if (e == null)
        return null;

      return new ErrorResponse {
        Message = e.Message,
        StackTrace = e.StackTrace,
        FullTypeName = e.GetType().FullName,
        InnerError = CreateErrorResponse(e.InnerException)
      };
    }

    public static ErrorResponseException CreateException(this ErrorResponse error) {
      return new ErrorResponseException(error);
    }

    public static ErrorResponse GetBaseError(this ErrorResponse error) {
      var result = error;
      while (result.InnerError != null)
        result = result.InnerError;
      return result;
    }

    public static bool IsOperationCanceled(this ErrorResponse error) {
      return GetBaseError(error).FullTypeName == typeof(OperationCanceledException).FullName;
    }
  }
}