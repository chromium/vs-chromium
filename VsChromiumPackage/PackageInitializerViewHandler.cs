// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromiumPackage.Commands;
using VsChromiumPackage.Views;

namespace VsChromiumPackage {
  /// <summary>
  /// Ensure the VsPackage is initialized as soon as a view is created. We need
  /// this because we rely on ITextDocumentFactory observers to track document
  /// opening/closing, and this is done in the VsPackage initialization code.
  /// </summary>
  [PartCreationPolicy(CreationPolicy.NonShared)]
  [Export(typeof(IViewHandler))]
  public class PackageInitializerViewHandler : IViewHandler {
    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider ServiceProvider = null; // Set via MEF

    [Import]
    internal IVsEditorAdaptersFactoryService AdapterService = null; // Set via MEF

    [Import]
    internal ITextDocumentService TextDocumentService = null; // Set via MEF

    [Import]
    internal ITextDocumentFactoryService TextDocumentFactoryService = null; // Set via MEF

    private bool _loaded;

    public int Priority { get { return int.MaxValue; } }

    public void Attach(IVsTextView textViewAdapter) {
      // Try loading only once since this is a heavy operation.
      if (_loaded)
        return;
      _loaded = true;

      var shell = ServiceProvider.GetService(typeof(SVsShell)) as IVsShell;
      if (shell == null)
        return;

      IVsPackage package = null;
      var packageToBeLoadedGuid = new Guid(GuidList.GuidVsChromiumPkgString);
      shell.LoadPackage(ref packageToBeLoadedGuid, out package);

      // Ensure document is seen as loaded - This is necessary for the first
      // opened editor because the document is open before the package has a
      // chance to listen to TextDocumentFactoryService events.
      var textView = AdapterService.GetWpfTextView(textViewAdapter);
      if (textView != null) {
        foreach (var buffer in textView.BufferGraph.GetTextBuffers(x => true)) {
          ITextDocument textDocument;
          if (TextDocumentFactoryService.TryGetTextDocument(buffer, out textDocument)) {
            TextDocumentService.OnDocumentOpen(textDocument);
          }
        }
      }
    }
  }
}
