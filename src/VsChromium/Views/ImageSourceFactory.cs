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
        bitmapImage.EndInit();
        return bitmapImage;
      });
    }

    private static Uri GetUri(string filePath) {
      // Note: The VS WPF designer requires an absolute URL for the images
      // to load properly.
      var uriString = string.Format(
        "pack://application:,,,/{0};component/{1}",
        Assembly.GetExecutingAssembly().GetName().Name,
        filePath);
      return new Uri(uriString, UriKind.Absolute);
    }
  }
}