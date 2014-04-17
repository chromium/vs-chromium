// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core {
  public class PackageServices {
    public const string DkmComponentEventHandlerId = "C11F9124-D0FF-4EFC-AD90-FE6121D7415A";
    public static readonly Guid DkmComponentEventHandler = new Guid(DkmComponentEventHandlerId);
  }
}
