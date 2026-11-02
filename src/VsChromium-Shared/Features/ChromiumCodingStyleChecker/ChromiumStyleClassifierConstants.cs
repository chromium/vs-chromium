// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows;
using System.Windows.Media;

namespace VsChromium.Features.ChromiumCodingStyleChecker {
  public static class ChromiumStyleClassifierConstants {
    public const string Name = "VsChromium-CodingStyleChecker";
    public const string DisplayName = "VsChromium: Coding Style Checker";
    public static Color BackgroundColor = Colors.Red;
    public static TextDecorationCollection TextDecorations = System.Windows.TextDecorations.Underline;
  }
}
