﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TextManager.Interop;

namespace VsChromium.Views {
  public interface IViewHandler {
    int Priority { get; }
    void Attach(IVsTextView textViewAdapter);
  }
}
