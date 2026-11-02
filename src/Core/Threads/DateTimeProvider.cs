// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;

namespace VsChromium.Core.Threads {
  [Export(typeof(IDateTimeProvider))]
  public class DateTimeProvider : IDateTimeProvider {
    public DateTime UtcNow { get { return DateTime.UtcNow; } }
  }
}
