using System;
using VsChromiumCore.Ipc;

namespace VsChromiumPackage.Threads {
  class IpcRequestException : Exception {
    private readonly IpcRequest _request;

    public IpcRequestException(IpcRequest request, Exception inner)
      : base(string.Format("Error sending request {0} of type {1} to server", request.RequestId, request.Data.GetType().FullName), inner) {
      _request = request;
    }

    public IpcRequest Request { get { return _request; } }
  }
}