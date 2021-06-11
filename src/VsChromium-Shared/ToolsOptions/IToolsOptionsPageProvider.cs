// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using VsChromium.Package;

namespace VsChromium.ToolsOptions {
  public interface IToolsOptionsPageProvider {
    T GetToolsOptionsPage<T>() where T : DialogPage, new();
  }

  [Export(typeof(IToolsOptionsPageProvider))]
  public class ToolsOptionsPageProvider : IToolsOptionsPageProvider {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public ToolsOptionsPageProvider(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public T GetToolsOptionsPage<T>() where T : DialogPage, new() {
      return _visualStudioPackageProvider.Package.GetToolsOptionsPage<T>();
    }
  }
}
