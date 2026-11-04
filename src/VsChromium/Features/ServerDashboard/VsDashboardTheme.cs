// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Logging;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>
  /// Reads the current Visual Studio theme (colors of the environment and of
  /// the common controls, environment font) for <see cref="DashboardTheme"/>.
  /// </summary>
  public static class VsDashboardTheme {
    private static readonly Dictionary<ThemeColor, ThemeResourceKey> Keys = new Dictionary<ThemeColor, ThemeResourceKey> {
      { ThemeColor.Background, EnvironmentColors.ToolWindowBackgroundColorKey },
      { ThemeColor.Foreground, EnvironmentColors.ToolWindowTextColorKey },
      { ThemeColor.Border, EnvironmentColors.ToolWindowBorderColorKey },
      { ThemeColor.Link, EnvironmentColors.ControlLinkTextColorKey },
      { ThemeColor.LinkHover, EnvironmentColors.ControlLinkTextHoverColorKey },
      { ThemeColor.Highlight, EnvironmentColors.SystemHighlightColorKey },
      { ThemeColor.ToolTipBackground, EnvironmentColors.ToolTipColorKey },
      { ThemeColor.ToolTipBorder, EnvironmentColors.ToolTipBorderColorKey },
      { ThemeColor.ToolTipForeground, EnvironmentColors.ToolTipTextColorKey },
      { ThemeColor.PrimaryButtonBackground, CommonControlsColors.ButtonDefaultColorKey },
      { ThemeColor.PrimaryButtonForeground, CommonControlsColors.ButtonDefaultTextColorKey },
      { ThemeColor.ButtonBackground, CommonControlsColors.ButtonColorKey },
      { ThemeColor.ButtonForeground, CommonControlsColors.ButtonTextColorKey },
      { ThemeColor.ButtonHoverBackground, CommonControlsColors.ButtonHoverColorKey },
      { ThemeColor.ButtonBorder, CommonControlsColors.ButtonBorderColorKey },
      { ThemeColor.CheckBoxBackground, CommonControlsColors.CheckBoxBackgroundColorKey },
      { ThemeColor.CheckBoxBorder, CommonControlsColors.CheckBoxBorderColorKey },
      { ThemeColor.CheckBoxGlyph, CommonControlsColors.CheckBoxGlyphColorKey },
    };

    /// <summary>The current theme (UI thread).</summary>
    public static DashboardThemeInput GetCurrentTheme(FrameworkElement element) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var input = new DashboardThemeInput {
        HighContrast = SystemParameters.HighContrast,
        EditorFontFamily = "Consolas",
      };
      foreach (var entry in Keys) {
        try {
          var color = VSColorTheme.GetThemedColor(entry.Value);
          input.Colors[entry.Key] = new ThemeRgba(color.R, color.G, color.B, color.A);
        }
        catch (Exception e) {
          Logger.LogWarn(e, "Error reading the theme color {0}.", entry.Key);
        }
      }
      var fontFamily = element.TryFindResource(VsFonts.EnvironmentFontFamilyKey) as FontFamily;
      input.FontFamily = fontFamily?.Source;
      var fontSize = element.TryFindResource(VsFonts.EnvironmentFontSizeKey);
      input.FontSize = fontSize is double ? (double)fontSize : 12;
      return input;
    }
  }
}
