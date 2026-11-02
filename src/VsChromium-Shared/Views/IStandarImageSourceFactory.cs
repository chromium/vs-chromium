// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows.Media;

namespace VsChromium.Views {
  public interface IStandarImageSourceFactory {
    ImageSource OpenFolder { get; }
    ImageSource ClosedFolder { get; }
    ImageSource GetImageForDocument(string path);
    ImageSource GetImage(string resourceName);
  }
}
