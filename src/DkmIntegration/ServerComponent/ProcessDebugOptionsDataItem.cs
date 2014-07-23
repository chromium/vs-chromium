// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using VsChromium.Core.DkmShared;

namespace VsChromium.DkmIntegration.ServerComponent {
  class ProcessDebugOptionsDataItem : DkmDataItem {
    private DebugProcessOptions _options;

    public ProcessDebugOptionsDataItem(DebugProcessOptions options) {
      this._options = options;
    }

    public DebugProcessOptions Options { get { return _options; } }
  }
}
