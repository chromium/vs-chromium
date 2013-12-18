// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromiumPackage.Features.ChromeDebug {
  static class GuidList {
    public const string GuidChromeDebugCmdSetString = "6608d840-ce6c-45ab-b856-eb0a0b471ff1";

    public static readonly Guid GuidChromeDebugCmdSet = new Guid(GuidChromeDebugCmdSetString);
  };
}
