// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>A color (red, green, blue, alpha).</summary>
  public struct ThemeRgba {
    public ThemeRgba(byte r, byte g, byte b, byte a = 255) {
      R = r;
      G = g;
      B = b;
      A = a;
    }

    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    /// <summary>Relative luminance (0: black, 1: white).</summary>
    public double Luminance => (0.2126 * R + 0.7152 * G + 0.0722 * B) / 255;

    /// <summary>"#rrggbb", or "rgba(r, g, b, a)" if not opaque.</summary>
    public string ToCss() {
      if (A == 255) {
        return string.Format(CultureInfo.InvariantCulture, "#{0:x2}{1:x2}{2:x2}", R, G, B);
      }
      return string.Format(CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, {3:0.###})", R, G, B, A / 255.0);
    }

    /// <summary>This color with an opacity of <paramref name="alpha"/> (0 to 1).</summary>
    public ThemeRgba WithAlpha(double alpha) {
      return new ThemeRgba(R, G, B, (byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255));
    }

    /// <summary>The opaque mix of this color (weight <paramref name="weight"/>) and <paramref name="other"/>.</summary>
    public ThemeRgba Mix(ThemeRgba other, double weight) {
      Func<byte, byte, byte> mix = (a, b) => (byte)Math.Round(a * weight + b * (1 - weight));
      return new ThemeRgba(mix(R, other.R), mix(G, other.G), mix(B, other.B));
    }
  }

  /// <summary>
  /// The colors of the Visual Studio theme used by the dashboard (resolved by
  /// the host from the theme resource keys, see <c>VsDashboardTheme</c>).
  /// </summary>
  public enum ThemeColor {
    /// <summary>Tool window background.</summary>
    Background,
    /// <summary>Tool window text.</summary>
    Foreground,
    /// <summary>Tool window border.</summary>
    Border,
    /// <summary>Hyperlinks.</summary>
    Link,
    LinkHover,
    /// <summary>Highlight (selection, focus, progress).</summary>
    Highlight,
    ToolTipBackground,
    ToolTipBorder,
    ToolTipForeground,
    /// <summary>Default (primary) button.</summary>
    PrimaryButtonBackground,
    PrimaryButtonForeground,
    /// <summary>Other buttons.</summary>
    ButtonBackground,
    ButtonForeground,
    ButtonHoverBackground,
    ButtonBorder,
    CheckBoxBackground,
    CheckBoxBorder,
    CheckBoxGlyph,
  }

  /// <summary>The theme of Visual Studio at a point in time (input of <see cref="DashboardTheme"/>).</summary>
  public class DashboardThemeInput {
    public IDictionary<ThemeColor, ThemeRgba> Colors { get; set; } = new Dictionary<ThemeColor, ThemeRgba>();
    /// <summary>The environment font (e.g. "Segoe UI").</summary>
    public string FontFamily { get; set; }
    /// <summary>The environment font size, in pixels (device independent).</summary>
    public double FontSize { get; set; }
    /// <summary>The editor font (paths), e.g. "Consolas".</summary>
    public string EditorFontFamily { get; set; }
    /// <summary>Windows is in high contrast mode.</summary>
    public bool HighContrast { get; set; }
  }

  /// <summary>
  /// Maps the theme of Visual Studio to the CSS variables of the dashboard page
  /// (the "--vscode-*" variables used by media/dashboard.css of the VS Code
  /// extension). Colors without equivalent in the Visual Studio theme (charts,
  /// errors, warnings) use the palettes of the VS Code light and dark themes,
  /// chosen from the luminance of the background.
  /// </summary>
  public static class DashboardTheme {
    private static readonly ThemeRgba White = new ThemeRgba(255, 255, 255);
    private static readonly ThemeRgba Black = new ThemeRgba(0, 0, 0);

    /// <summary>The variables of the page for <paramref name="input"/> (sorted by name).</summary>
    public static SortedDictionary<string, string> BuildVariables(DashboardThemeInput input) {
      var colors = input.Colors;
      Func<ThemeColor, ThemeRgba, ThemeRgba> get = (key, fallback) => {
        ThemeRgba value;
        return colors.TryGetValue(key, out value) ? value : fallback;
      };
      var background = get(ThemeColor.Background, White);
      var dark = background.Luminance < 0.5;
      var foreground = get(ThemeColor.Foreground, dark ? new ThemeRgba(0xcc, 0xcc, 0xcc) : new ThemeRgba(0x1e, 0x1e, 0x1e));
      var border = get(ThemeColor.Border, foreground.WithAlpha(0.35));
      var highlight = get(ThemeColor.Highlight, new ThemeRgba(0x00, 0x7a, 0xcc));
      var link = get(ThemeColor.Link, highlight);
      var description = foreground.Mix(background, 0.72);

      var v = new SortedDictionary<string, string>(StringComparer.Ordinal);
      Action<string, ThemeRgba> set = (name, color) => v["--vscode-" + name] = color.ToCss();

      // Text and background
      set("foreground", foreground);
      set("editor-background", background);
      set("descriptionForeground", description);
      set("settings-headerForeground", foreground);
      set("errorForeground", dark ? new ThemeRgba(0xf4, 0x87, 0x71) : new ThemeRgba(0xa1, 0x26, 0x0d));
      set("editorWarning-foreground", dark ? new ThemeRgba(0xcc, 0xa7, 0x00) : new ThemeRgba(0xbf, 0x88, 0x03));

      // Borders and focus
      set("panel-border", border);
      set("settings-headerBorder", border);
      set("editorWidget-border", border);
      set("focusBorder", highlight);

      // Links
      set("textLink-foreground", link);
      set("textLink-activeForeground", get(ThemeColor.LinkHover, link));

      // Buttons
      var primary = get(ThemeColor.PrimaryButtonBackground, highlight);
      var primaryForeground = get(ThemeColor.PrimaryButtonForeground, White);
      var button = get(ThemeColor.ButtonBackground, foreground.Mix(background, 0.15));
      var buttonHover = get(ThemeColor.ButtonHoverBackground, foreground.Mix(background, 0.25));
      set("button-background", primary);
      set("button-foreground", primaryForeground);
      set("button-hoverBackground", primary.Mix(dark ? White : Black, 0.88));
      set("button-secondaryBackground", button);
      set("button-secondaryForeground", get(ThemeColor.ButtonForeground, foreground));
      set("button-secondaryHoverBackground", buttonHover);

      // Check boxes (the Settings editor uses the "settings-checkbox*" variables)
      var checkBoxBackground = get(ThemeColor.CheckBoxBackground, background);
      var checkBoxBorder = get(ThemeColor.CheckBoxBorder, border);
      var checkBoxGlyph = get(ThemeColor.CheckBoxGlyph, foreground);
      set("checkbox-background", checkBoxBackground);
      set("checkbox-border", checkBoxBorder);
      set("checkbox-foreground", checkBoxGlyph);
      set("settings-checkboxBackground", checkBoxBackground);
      set("settings-checkboxBorder", checkBoxBorder);
      set("settings-checkboxForeground", checkBoxGlyph);

      // Lists, tables, hovers
      set("list-hoverBackground", foreground.WithAlpha(dark ? 0.08 : 0.06));
      set("settings-rowHoverBackground", foreground.WithAlpha(dark ? 0.04 : 0.03));
      set("keybindingTable-headerBackground", foreground.WithAlpha(0.04));
      set("keybindingTable-rowsBackground", foreground.WithAlpha(0.04));
      set("badge-background", foreground.Mix(background, 0.3));
      set("badge-foreground", background);
      set("editorHoverWidget-background", get(ThemeColor.ToolTipBackground, background));
      set("editorHoverWidget-border", get(ThemeColor.ToolTipBorder, border));
      set("editorHoverWidget-foreground", get(ThemeColor.ToolTipForeground, foreground));
      set("widget-shadow", Black.WithAlpha(dark ? 0.36 : 0.16));
      set("progressBar-background", highlight);
      set("inputValidation-errorBackground", dark ? new ThemeRgba(0x5a, 0x1d, 0x1d) : new ThemeRgba(0xf2, 0xde, 0xde));
      set("inputValidation-errorBorder", new ThemeRgba(0xbe, 0x11, 0x00));
      set("inputValidation-errorForeground", foreground);

      // Charts (the palettes of the VS Code themes)
      set("charts-blue", dark ? new ThemeRgba(0x37, 0x94, 0xff) : new ThemeRgba(0x1a, 0x85, 0xff));
      set("charts-green", dark ? new ThemeRgba(0x89, 0xd1, 0x85) : new ThemeRgba(0x38, 0x8a, 0x34));
      set("charts-yellow", dark ? new ThemeRgba(0xcc, 0xa7, 0x00) : new ThemeRgba(0xbf, 0x88, 0x03));
      set("charts-orange", new ThemeRgba(0xd1, 0x86, 0x16));
      set("charts-purple", dark ? new ThemeRgba(0xb1, 0x80, 0xd7) : new ThemeRgba(0x65, 0x2d, 0x90));
      set("charts-red", dark ? new ThemeRgba(0xf1, 0x4c, 0x4c) : new ThemeRgba(0xe5, 0x14, 0x00));
      set("testing-iconPassed", dark ? new ThemeRgba(0x89, 0xd1, 0x85) : new ThemeRgba(0x38, 0x8a, 0x34));

      // High contrast: explicit borders (the page draws them only when these are defined).
      if (input.HighContrast) {
        set("contrastBorder", get(ThemeColor.Border, foreground));
        set("contrastActiveBorder", highlight);
        set("button-border", get(ThemeColor.ButtonBorder, foreground));
      }

      // Fonts
      v["--vscode-font-family"] = CssFontFamily(input.FontFamily, "system-ui, sans-serif");
      v["--vscode-font-size"] = string.Format(CultureInfo.InvariantCulture, "{0:0.##}px",
        input.FontSize > 0 ? input.FontSize : 12);
      v["--vscode-font-weight"] = "normal";
      v["--vscode-editor-font-family"] = CssFontFamily(input.EditorFontFamily, "Consolas, \"Courier New\", monospace");
      return v;
    }

    /// <summary>A CSS font family list: the family (quoted), then <paramref name="fallback"/>.</summary>
    public static string CssFontFamily(string family, string fallback) {
      if (string.IsNullOrWhiteSpace(family)) {
        return fallback;
      }
      // Note: Quotes, semicolons and braces are removed (the value must stay a single declaration).
      var cleaned = family.Replace("\"", "").Replace(";", "").Replace("{", "").Replace("}", "").Trim();
      return string.Format("\"{0}\", {1}", cleaned, fallback);
    }
  }
}
