// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Utility;

namespace VsChromium.Views {
  /// <summary>
  /// Expose VS Themed colors as static properties
  /// </summary>
  public class VsColors {

    private static VsBrushKey _searchMatchHighlightForeground;
    public static VsBrushKey SearchMatchHighlightForeground {
      get {
        return _searchMatchHighlightForeground ?? 
          (_searchMatchHighlightForeground = new VsBrushKey(
            ReflectionUtils.GetPropertyName(() => VsColors.SearchMatchHighlightForeground),
            new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00))));
      }
    }

    private static VsBrushKey _searchMatchHighlightBackground;
    public static VsBrushKey SearchMatchHighlightBackground {
      get {
        return _searchMatchHighlightBackground ?? 
          (_searchMatchHighlightBackground = new VsBrushKey(
            ReflectionUtils.GetPropertyName(() => VsColors.SearchMatchHighlightBackground),
            new SolidColorBrush(Color.FromRgb(0xfd, 0xfb, 0xac))));
      }
    }

    public static ThemeResourceKey SelectedTreeViewItem {
      get { return EnvironmentColors.ToolTipBorderBrushKey; }
    }

    public static ResourceDictionary BuildResourceDictionary() {
      var infos = typeof (VsColors)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(x => x.PropertyType == typeof(VsBrushKey));
      var result = new ResourceDictionary();
      foreach (var info in infos) {
        var key = info.GetValue(null) as VsBrushKey;
        if (key != null) {
          result.Add(key, key.Brush);
        }
      }
      return result;
    }

    public class VsBrushKey : IEquatable<VsBrushKey> {
      private readonly string _name;
      private readonly Brush _brush;

      public VsBrushKey(string name, Brush brush) {
        _name = name;
        _brush = brush;
      }

      public Brush Brush {
        get { return _brush; }
      }

      public bool Equals(VsBrushKey other) {
        if (other == null)
          return false;
        return this._name == other._name;
      }

      public override bool Equals(object obj) {
        return this.Equals(obj as VsBrushKey);
      }

      public override int GetHashCode() {
        return _name.GetHashCode();
      }
    }
  }
}