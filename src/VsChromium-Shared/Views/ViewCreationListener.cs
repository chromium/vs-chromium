﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace VsChromium.Views {
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  [ContentType("text")]
  [Export(typeof(IVsTextViewCreationListener))]
  class ViewCreationListener : IVsTextViewCreationListener {
    [Import]
    internal IVsEditorAdaptersFactoryService AdapterService = null; // Set via MEF

    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider ServiceProvider = null; // Set via MEF

    private class Marker {
      private readonly List<IViewHandler> _handlers = new List<IViewHandler>();

      public void AddHandler(IViewHandler handler) {
        _handlers.Add(handler);
      }
    }

    public void VsTextViewCreated(IVsTextView textViewAdapter) {
      IWpfTextView textView = AdapterService.GetWpfTextView(textViewAdapter);
      if (textView == null) {
        return;
      }
      Func<Marker> creator = () => {
        var result = new Marker();
        var componentModel = (IComponentModel)ServiceProvider.GetService(typeof(SComponentModel));
        var handlers = componentModel.DefaultExportProvider
          .GetExportedValues<IViewHandler>()
          .OrderByDescending(x => x.Priority)
          .ToList();
        foreach (var handler in handlers) {
          handler.Attach(textViewAdapter);
          result.AddHandler(handler);
        }
        return result;
      };
      textView.Properties.GetOrCreateSingletonProperty<Marker>(creator);
    }
  }
}
