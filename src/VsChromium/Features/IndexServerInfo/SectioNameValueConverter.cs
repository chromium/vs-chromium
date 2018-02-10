// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Globalization;
using System.Windows.Data;

namespace VsChromium.Features.IndexServerInfo {
  public class SectioNameValueConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var s = value as string;
      if (string.IsNullOrEmpty(s)) {
        return "n/a";
      }

      return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }
}