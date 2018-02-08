// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Globalization;
using System.Windows.Data;

namespace VsChromium.Features.ToolWindows.CodeSearch.IndexServerInfo {
  public class HumanReadableBytesValueConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var byteCount = (long)value;

      if (byteCount < 1024) {
        return $"{byteCount:n0} Bytes";
      }

      if (byteCount < 1024 * 1024) {
        return $"{(double) byteCount / 1024:n2} KB";
      }

      if (byteCount < 1024L * 1024 * 1024) {
        return $"{(double) byteCount / 1024 / 1024:n2} MB";
      }

      if (byteCount < 1024L * 1024 * 1024 * 1024) {
        return $"{(double) byteCount / 1024 / 1024 / 1024:n2} GB";
      }

      return byteCount;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }
}