// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows;
using System.Windows.Media;

namespace VsChromiumPackage.Features.BuildErrors {
  public static class BuildOutputClassifierConstants {
    public const string Name = "VsChromium-BuildOutput";
    public const string DisplayName = "VsChromium: Build Output";
    public static Color ForegroundColor = Colors.Blue;
    public static TextDecorationCollection TextDecorations = System.Windows.TextDecorations.Underline;
  }
}
