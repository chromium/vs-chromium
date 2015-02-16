// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Package;

namespace VsChromium.Features.SourceExplorerHierarchy {
  [Export(typeof(IVsGlyphService))]
  public class VsGlyphService : IVsGlyphService {
    private readonly Lazy<IntPtr> _imageListPtr;
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public VsGlyphService(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
      this._imageListPtr = new Lazy<IntPtr>(GetImageListPtr);
    }

    public IntPtr ImageListPtr {
      get {
        return _imageListPtr.Value;
      }
    }

    public int GetImageIndex(StandardGlyphGroup standardGlyphGroup, StandardGlyphItem standardGlyphItem) {
      if (standardGlyphGroup >= StandardGlyphGroup.GlyphGroupError)
        return (int)standardGlyphGroup;
      return (int)standardGlyphGroup + (int)standardGlyphItem;

    }

    private IntPtr GetImageListPtr() {
      var vsShell = _visualStudioPackageProvider.Package.ServiceProvider.GetService(typeof(SVsShell)) as IVsShell;
      if (vsShell == null)
        throw new InvalidOperationException();

      object pvar;
      if (ErrorHandler.Failed(vsShell.GetProperty(-9027, out pvar)) || pvar == null)
        throw new InvalidOperationException();

      return Unbox.AsIntPtr(pvar);
    }
  }
}
