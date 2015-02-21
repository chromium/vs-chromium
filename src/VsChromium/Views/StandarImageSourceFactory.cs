// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;

namespace VsChromium.Views {
  [Export(typeof(IStandarImageSourceFactory))]
  public class StandarImageSourceFactory : IStandarImageSourceFactory {
    private readonly IGlyphService _glyphService;
    private readonly IImageSourceFactory _imageSourceFactory;

    [ImportingConstructor]
    public StandarImageSourceFactory(IGlyphService glyphService, IImageSourceFactory imageSourceFactory) {
      _glyphService = glyphService;
      _imageSourceFactory = imageSourceFactory;
    }

    public ImageSource OpenFolder { 
      get { 
        return _glyphService.GetGlyph(
            StandardGlyphGroup.GlyphOpenFolder, 
            StandardGlyphItem.GlyphItemPublic); 
      } 
    }

    public ImageSource ClosedFolder { 
      get { 
        return _glyphService.GetGlyph(
            StandardGlyphGroup.GlyphClosedFolder, 
            StandardGlyphItem.GlyphItemPublic); 
      } 
    }

    public ImageSource GetImageForDocument(string path) {
      return GetImage("TextDocument");
    }

    public ImageSource GetImage(string resourceName) {
      return _imageSourceFactory.GetImage(resourceName);
    }
  }
}
