// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using System.Linq.Expressions;
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
  public class VsResources {
    private static VsResourceKey CreateKey(
      Expression<Func<VsResourceKey>> property,
      Color color) {
      return new VsResourceKey(
        ReflectionUtils.GetPropertyName(property),
        new SolidColorBrush(color));
    }

    private static VsResourceKey _searchMatchHighlightForeground;
    public static VsResourceKey SearchMatchHighlightForeground {
      get {
        return _searchMatchHighlightForeground ??
          (_searchMatchHighlightForeground = CreateKey(
            () => SearchMatchHighlightForeground,
            Color.FromRgb(0x00, 0x00, 0x00)));
      }
    }

    private static VsResourceKey _searchMatchHighlightBackground;
    public static VsResourceKey SearchMatchHighlightBackground {
      get {
        return _searchMatchHighlightBackground ??
          (_searchMatchHighlightBackground = CreateKey(
            () => SearchMatchHighlightBackground,
            Color.FromRgb(0xfd, 0xfb, 0xac)));
      }
    }

    public static ThemeResourceKey SelectedItemBackground {
      get { return EnvironmentColors.ToolTipBorderBrushKey; }
    }

    public static ResourceDictionary BuildResourceDictionary() {
      var infos = typeof(VsResources)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(x => x.PropertyType == typeof(VsResourceKey));
      var result = new ResourceDictionary();
      foreach (var info in infos) {
        var key = info.GetValue(null) as VsResourceKey;
        if (key != null) {
          result.Add(key, key.Brush);
        }
      }
      return result;
    }

    /// <summary>
    /// WPF resource key, as well as associated value.
    /// </summary>
    public class VsResourceKey : IEquatable<VsResourceKey> {
      private readonly string _name;
      private readonly Brush _brush;

      public VsResourceKey(string name, Brush brush) {
        _name = name;
        _brush = brush;
      }

      public Brush Brush {
        get { return _brush; }
      }

      public bool Equals(VsResourceKey other) {
        if (other == null)
          return false;
        return this._name == other._name;
      }

      public override bool Equals(object obj) {
        return this.Equals(obj as VsResourceKey);
      }

      public override int GetHashCode() {
        return _name.GetHashCode();
      }
    }
  }
}