// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.DkmShared {
  public class PackageServices {
    public const string DkmComponentEventHandlerId = "C11F9124-D0FF-4EFC-AD90-FE6121D7415A";
    public static readonly Guid DkmComponentEventHandler = new Guid(DkmComponentEventHandlerId);

    public const string VsPackageMessageId = "A2D51FDA-7CC6-4469-BA5C-1BA83D22629A";
    public static readonly Guid VsPackageMessageGuid = new Guid(VsPackageMessageId);

    public const string VsDebuggerMessageId = "1F9230BF-637C-42EC-8951-69B003AFB9D7";
    public static readonly Guid VsDebuggerMessageGuid = new Guid(VsDebuggerMessageId);
  }
}
