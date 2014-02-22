using System;

namespace VsChromiumCore.Ipc {
  public class ErrorResponseException : Exception {
    private readonly ErrorResponse _errorResponse;

    public ErrorResponseException(ErrorResponse errorResponse)
      : base(errorResponse.Message) {
      _errorResponse = errorResponse;
    }

    public ErrorResponse ErrorResponse { get { return _errorResponse; } }
  }
}