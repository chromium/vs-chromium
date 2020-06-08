// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;

namespace VsChromium.Views {
  /// <summary>
  /// Maintains list of active files in Visual Studio.
  /// This is essentially an abstraction over <see cref="IVsRunningDocumentTable"/> that ensures
  /// we have access to the <see cref="FullPath"/> and <see cref="ITextDocument"/> of every file.
  /// </summary>
  public interface ITextDocumentTable {
    /// <summary>
    /// Returns the <see cref="OpenDocument"/> for a given file path, or null if there is
    /// no document open for that file.
    /// </summary>
    OpenDocument GetOpenDocument(FullPath path);

    /// <summary>
    /// Returns the list of currently opened document in Visual Studio editor.
    /// </summary>
    IList<OpenDocument> GetOpenDocuments();

    event EventHandler<OpenDocumentEventArgs> OpenDocumentCreated;
    event EventHandler<OpenDocumentEventArgs> OpenDocumentClosed;
    event EventHandler<OpenDocumentEventArgs> OpenDocumentSavedToDisk;
    event EventHandler<OpenDocumentEventArgs> OpenDocumentLoadedFromDisk;
    event EventHandler<OpenDocumentRenamedEventArgs> OpenDocumentRenamed;
  }

  public class OpenDocumentEventArgs : EventArgs {
    public OpenDocumentEventArgs(OpenDocument openDocument) {
      OpenDocument = openDocument;
    }
    public OpenDocument OpenDocument { get; private set; }
  }

  public class OpenDocumentRenamedEventArgs : OpenDocumentEventArgs {
    public OpenDocumentRenamedEventArgs(FullPath oldPath, OpenDocument document)
      : base(document) {
      OldPath = oldPath;
    }
    public FullPath OldPath { get; private set; }
    public FullPath NewPath { get { return OpenDocument.Path; } }
  }

  public class OpenDocument {
    private readonly FullPath _path;
    private readonly ITextDocument _textDocument;

    public OpenDocument(FullPath path, ITextDocument textDocument) {
      Invariants.CheckArgument(path != default(FullPath), "path", "Path should not be empty");
      Invariants.CheckArgumentNotNull(textDocument, "textDocument");
      _path = path;
      _textDocument = textDocument;
    }

    public FullPath Path { get { return _path; } }
    public ITextDocument TextDocument { get { return _textDocument; } }
  }
}
