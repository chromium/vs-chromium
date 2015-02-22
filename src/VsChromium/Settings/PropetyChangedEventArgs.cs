// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Settings {
  public class PropetyChangedEventArgs<T> : EventArgs {
    public T OldValue { get; set; }
    public T NewValue { get; set; }
  }
}