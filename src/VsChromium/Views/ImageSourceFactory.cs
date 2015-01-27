// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;

namespace VsChromium.Views {
  [Export(typeof(IImageSourceFactory))]
  public class ImageSourceFactory : IImageSourceFactory {
    private static readonly Lazy<IImageSourceFactory> InstanceFactory =
      new Lazy<IImageSourceFactory>(() => new ImageSourceFactory());

    public static IImageSourceFactory Instance {
      get { return InstanceFactory.Value; }
    }

    private readonly ConcurrentDictionary<string, BitmapImage> _images =
      new ConcurrentDictionary<string, BitmapImage>(SystemPathComparer.Instance.StringComparer);

    public ImageSource GetImage(string resourceName) {
      return _images.GetOrAdd(resourceName, s => {
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.UriSource = GetUri(string.Format("Views/Images/{0}.png", resourceName));
        //bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        return bitmapImage;
      });
    }

    private static Uri GetUri(string filePath) {
      var uriString = string.Format("/{0};component/{1}",
        Assembly.GetExecutingAssembly().GetName().Name, filePath);
      //Logger.Log("GetImage: {0}", uriString);
      return new Uri(uriString, UriKind.Relative);
    }
  }
}