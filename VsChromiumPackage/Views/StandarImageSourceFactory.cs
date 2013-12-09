// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using VsChromiumCore.FileNames;

namespace VsChromiumPackage.Views {
  [Export(typeof(IStandarImageSourceFactory))]
  public class StandarImageSourceFactory : IStandarImageSourceFactory {
    private readonly IGlyphService _glyphService;

    private readonly ConcurrentDictionary<string, BitmapImage> _images =
        new ConcurrentDictionary<string, BitmapImage>(SystemPathComparer.Instance.Comparer);

    [ImportingConstructor]
    public StandarImageSourceFactory(IGlyphService glyphService) {
      this._glyphService = glyphService;
    }

    public ImageSource OpenFolder {
      get {
        return this._glyphService.GetGlyph(StandardGlyphGroup.GlyphOpenFolder, StandardGlyphItem.GlyphItemPublic);
      }
    }

    public ImageSource ClosedFolder {
      get {
        return this._glyphService.GetGlyph(StandardGlyphGroup.GlyphClosedFolder, StandardGlyphItem.GlyphItemPublic);
      }
    }

    public ImageSource GetImageForDocument(string path) {
      return GetImage("TextDocument");
    }

    public ImageSource GetImage(string resourceName) {
      return this._images.GetOrAdd(resourceName, s => {
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.UriSource = GetUri(string.Format("Views/Images/{0}.png", resourceName));
        bitmapImage.EndInit();
        return bitmapImage;
      });
    }

    private static Uri GetUri(string filePath) {
      var uriString = string.Format("/{0};component/{1}",
          Assembly.GetExecutingAssembly().GetName().Name, filePath);
      return new Uri(uriString, UriKind.Relative);
    }
  }
}
