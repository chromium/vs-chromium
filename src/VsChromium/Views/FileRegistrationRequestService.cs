// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Threads;

namespace VsChromium.Views {
  [Export(typeof(IFileRegistrationRequestService))]
  public class FileRegistrationRequestService : IFileRegistrationRequestService {
    private readonly IUIRequestProcessor _uiRequestProcessor;
    private readonly IFileSystem _fileSystem;

    [ImportingConstructor]
    public FileRegistrationRequestService(
      IUIRequestProcessor uiRequestProcessor,
      IFileSystem fileSystem) {
      _uiRequestProcessor = uiRequestProcessor;
      _fileSystem = fileSystem;
    }

    public void RegisterFile(string path) {
      SendRegisterFileRequest(path);
    }

    public void UnregisterFile(string path) {
      SentUnregisterFileRequest(path);
    }

    private void SendRegisterFileRequest(string path) {
      if (!IsPhysicalFile(path))
        return;

      var request = new UIRequest {
        Id = "RegisterFileRequest-" + path,
        Request = new RegisterFileRequest {
          FileName = path
        }
      };

      _uiRequestProcessor.Post(request);
    }

    private void SentUnregisterFileRequest(string path) {
      if (!IsValidPath(path))
        return;

      var request = new UIRequest {
        Id = "UnregisterFileRequest-" + path,
        Request = new UnregisterFileRequest {
          FileName = path
        }
      };

      _uiRequestProcessor.Post(request);
    }

    private bool IsValidPath(string path) {
      // This can happen with "Find in files" for example, as it uses a fake filename.
      if (!PathHelpers.IsAbsolutePath(path))
        return false;

      if (!PathHelpers.IsValidBclPath(path))
        return false;

      return true;
    }

    private bool IsPhysicalFile(string path) {
      // This can happen with "Find in files" for example, as it uses a fake filename.
      return IsValidPath(path) && _fileSystem.FileExists(new FullPath(path));
    }
  }
}