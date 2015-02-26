// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Package;
using VsChromium.Threads;

namespace VsChromium.Views {
  [Export(typeof(IFileRegistrationRequestService))]
  public class FileRegistrationRequestService : IFileRegistrationRequestService {
    private readonly IUIRequestProcessor _uiRequestProcessor;
    private readonly IFileSystem _fileSystem;
    private readonly IEventBus _eventBus;
    private readonly ConcurrentDictionary<ITextDocument, EventHandler<TextContentChangedEventArgs>> _documents =
      new ConcurrentDictionary<ITextDocument, EventHandler<TextContentChangedEventArgs>>(ReferenceEqualityComparer<ITextDocument>.Instance);
      
    [ImportingConstructor]
    public FileRegistrationRequestService(
      IUIRequestProcessor uiRequestProcessor,
      IFileSystem fileSystem,
      IEventBus eventBus) {
      _uiRequestProcessor = uiRequestProcessor;
      _fileSystem = fileSystem;
      _eventBus = eventBus;
    }

    public void RegisterTextDocument(ITextDocument document) {
      SendRegisterFileRequest(document.FilePath);
      if (!_documents.ContainsKey(document)) {
        var handler = _documents.GetOrAdd(document, (s, e) => 
          TextBufferOnChangedLowPriority(document, e));
        document.TextBuffer.ChangedLowPriority += handler;
      }
    }

    public void UnregisterTextDocument(ITextDocument document) {
      SendUnregisterFileRequest(document.FilePath);

      EventHandler<TextContentChangedEventArgs> handler;
      if (_documents.TryRemove(document, out handler)) {
        document.TextBuffer.ChangedLowPriority -= handler;
      }
    }

    private void TextBufferOnChangedLowPriority(ITextDocument textDocument, TextContentChangedEventArgs args) {
      _eventBus.Fire("TextBufferChanged", textDocument, args);
    }

    public void RegisterFile(string path) {
      SendRegisterFileRequest(path);
    }

    public void UnregisterFile(string path) {
      SendUnregisterFileRequest(path);
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

    private void SendUnregisterFileRequest(string path) {
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