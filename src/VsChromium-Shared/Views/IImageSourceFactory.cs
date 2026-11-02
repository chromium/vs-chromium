// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Drawing;
using System.Windows.Media;

namespace VsChromium.Views {
  public interface IImageSourceFactory {
    ImageSource GetImageSource(string resourceName);
    Icon GetIcon(string resourceName);
    ImageSource GetFileExtensionImageSource(string fileExtension);
    Icon GetFileExtensionIcon(string fileExtension);
  }
}