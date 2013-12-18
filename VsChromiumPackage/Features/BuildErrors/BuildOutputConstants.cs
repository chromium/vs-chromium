// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows;
using System.Windows.Media;

namespace VsChromiumPackage.Features.BuildErrors {
  public static class BuildOutputConstants {
    public const string ClassifierName = "VsChromiumPackageBuildOutput";
    public const string ClassifierDisplayName = "VsChromium: Build Errors";
    public static Color ClassifierForegroundColor = Colors.Blue;
    public static TextDecorationCollection ClassifierTextDecorations = TextDecorations.Underline;
  }
}
