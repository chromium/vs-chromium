// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumPackage.Views {
  [TextViewRole("EDITABLE"), ContentType("text"), Export(typeof(IVsTextViewCreationListener))]
  internal class ViewCreationListener : IVsTextViewCreationListener {
    [Import]
    internal IVsEditorAdaptersFactoryService AdapterService = null; // Set via MEF

    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider ServiceProvider = null; // Set via MEF

    class Marker {
      private readonly List<IViewHandler> _handlers = new List<IViewHandler>();

      public void AddHandler(IViewHandler handler) {
        this._handlers.Add(handler);
      }
    }

    public void VsTextViewCreated(IVsTextView textViewAdapter) {
      IWpfTextView textView = this.AdapterService.GetWpfTextView(textViewAdapter);
      if (textView == null) {
        return;
      }
      Func<Marker> creator = () => {
        var result = new Marker();
        var componentModel = (IComponentModel)this.ServiceProvider.GetService(typeof(SComponentModel));
        foreach(var handler in componentModel.DefaultExportProvider.GetExportedValues<IViewHandler>())
        {
          handler.Attach(textViewAdapter);
          result.AddHandler(handler);
        }
        return result;
      };
      textView.Properties.GetOrCreateSingletonProperty<Marker>(creator);
    }
  }
}
