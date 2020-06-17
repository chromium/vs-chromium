// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;

namespace VsChromium.Views {
  /// <summary>
  /// Keeps track of <see cref="ITextDocument"/> instances open in the Running Document Table of Visual Studio
  /// </summary>
  public interface ITextDocumentTable : IDisposable {
    /// <summary>
    /// Returns the <see cref="ITextDocument"/> instance for a given <paramref name="path"/>,
    /// or <code>null</code> if the file is not currently present in the Running Document Table.
    /// </summary>
    ITextDocument GetOpenDocument(FullPath path);

    /// <summary>
    /// Returns the list of <see cref="ITextDocument"/> instances currently present
    /// in the Running Document Table.
    /// </summary>
    IList<ITextDocument> GetOpenDocuments();

    /// <summary>
    /// Invoked when a <see cref="ITextDocument"/> has been added to the
    /// Running Document Table and after its contents is fully initialized,
    /// i.e. its contents has been loaded from disk.
    /// </summary>
    event EventHandler<TextDocumentEventArgs> TextDocumentOpened;
    /// <summary>
    /// Invoked when a text document has been closed and removed from the
    /// Running Document Table. The <see cref="TextDocumentEventArgs.TextDocument"/>
    /// instance can only be used for basic behavior, as most of its state has been
    /// disposed.
    /// </summary>
    event EventHandler<TextDocumentEventArgs> TextDocumentClosed;
    /// <summary>
    /// Invoked when a <see cref="ITextDocument"/> present in the Running Document Table
    /// has been renamed. The new and old path are in the <see cref="TextDocumentRenamedEventArgs"/>
    /// argument.
    /// </summary>
    event EventHandler<TextDocumentRenamedEventArgs> TextDocumentRenamed;
  }

  public class TextDocumentEventArgs : EventArgs {
    public TextDocumentEventArgs(FullPath fullPath, ITextDocument textDocument) {
      FullPath = fullPath;
      TextDocument = textDocument;
    }

    public FullPath FullPath { get; }
    public ITextDocument TextDocument { get; }
  }

  public class TextDocumentRenamedEventArgs : EventArgs {
    public TextDocumentRenamedEventArgs(ITextDocument textDocument, FullPath oldPath, FullPath newPath) {
      TextDocument = textDocument;
      OldPath = oldPath;
      NewPath = newPath;
    }

    public ITextDocument TextDocument { get; }
    public FullPath OldPath { get; }
    public FullPath NewPath { get; }
  }
}
