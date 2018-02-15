// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.Internal.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Win32.Shell;

namespace VsChromium.Views {
  [Export(typeof(IImageSourceFactory))]
  public class ImageSourceFactory : IImageSourceFactory {
    private static readonly Lazy<IImageSourceFactory> InstanceFactory =
      new Lazy<IImageSourceFactory>(() => new ImageSourceFactory());

    public static IImageSourceFactory Instance => InstanceFactory.Value;

    /// <summary>
    /// Resource name -> <see cref="BitmapSource"/>
    /// </summary>
    private readonly IDictionary<string, BitmapImage> _resourceImages =
      new Dictionary<string, BitmapImage>(SystemPathComparer.Instance.StringComparer);

    /// <summary>
    /// Resource name -> <see cref="Icon"/>
    /// </summary>
    private readonly IDictionary<string, KeyValuePair<SafeIconHandle, Icon>> _resourceIcons =
      new Dictionary<string, KeyValuePair<SafeIconHandle, Icon>>(SystemPathComparer.Instance.StringComparer);

    /// <summary>
    /// File Extension -> (ImageId, <see cref="ImageSource"/>)
    /// </summary>
    private readonly IDictionary<string, KeyValuePair<string, ImageSource>> _fileExtensionImageSources =
      new Dictionary<string, KeyValuePair<string, ImageSource>>(SystemPathComparer.Instance.StringComparer);

    /// <summary>
    /// ImageId -> <see cref="Icon"/>
    /// </summary>
    private readonly IDictionary<string, KeyValuePair<SafeIconHandle, Icon>> _imageSourceIdIcons =
      new Dictionary<string, KeyValuePair<SafeIconHandle, Icon>>(SystemPathComparer.Instance.StringComparer);

    public ImageSource GetImageSource(string resourceName) {
      return _resourceImages.GetOrAdd(resourceName, name => {
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.UriSource = GetUri(string.Format("Views/Images/{0}.png", name));
        bitmapImage.EndInit();
        return bitmapImage;
      });
    }

    public Icon GetIcon(string resourceName) {
      return _resourceIcons.GetOrAdd(resourceName, name => {
        var image = GetImageSource(name);
        return ImageSourceToIcon(image);
      }).Value;
    }

    public ImageSource GetFileExtensionImageSource(string fileExtension) {
      return GetImageSourceForFileExtension(fileExtension).Value;
    }

    public Icon GetFileExtensionIcon(string fileExtension) {
      var kvp = _fileExtensionImageSources.GetOrAdd(fileExtension, GetImageSourceForFileExtension);

      return _imageSourceIdIcons.GetOrAdd(kvp.Key, kvp.Value, (key, value) => {
        Logger.LogInfo("Creating icon for resource id {0}", key);
        return ImageSourceToIcon(value);
      }).Value;
    }

    private KeyValuePair<string, ImageSource> GetImageSourceForFileExtension(string fileExtension) {
      var list = DefaultIconImageList.Instance;
      ImageSource source = list.GetImage(fileExtension);
      if (source != null) {
        return new KeyValuePair<string, ImageSource>(fileExtension, source);
      }

      source = list.GetImage(".txt");
      if (source != null) {
        return new KeyValuePair<string, ImageSource>(".txt", source);
      }

      source = GetImageSource("TextDocument");
      Invariants.CheckOperation(source != null, "Text Document icon is missing");
      return new KeyValuePair<string, ImageSource>("TextDocument", source);
    }

    private static KeyValuePair<SafeIconHandle, Icon> ImageSourceToIcon(ImageSource source) {
      var image = source as BitmapSource;
      if (image == null)
        throw new InvalidOperationException();
      IntPtr hIcon = ImageHelper.BitmapFromBitmapSource(image).GetHicon();
      var iconHandle = new SafeIconHandle(hIcon);
      return new KeyValuePair<SafeIconHandle, Icon>(iconHandle, Icon.FromHandle(hIcon));
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