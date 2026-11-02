// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
#if !NETCOREAPP
using System.ComponentModel.Composition;
#endif

namespace VsChromium.Core.Threads {
#if !NETCOREAPP
  [Export(typeof(IDateTimeProvider))]
#endif
  public class DateTimeProvider : IDateTimeProvider {
    public DateTime UtcNow { get { return DateTime.UtcNow; } }
  }
}
