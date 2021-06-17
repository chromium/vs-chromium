﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Commands {
  static class GuidList {
    public const string GuidVsChromiumPkgString = "a10cf7af-5f0a-4502-b44b-51ff1b7c8a87";
    public const string GuidVsChromiumCmdSetString = "1d4bc583-de49-4113-af8d-81c62fd4b61b";
    public const string GuidCodeSearchToolWindowString = "42a7d178-f0a2-4981-802f-d0589707d174";
    public const string GuidBuildExplorerToolWindowString = "2A181862-CDB1-41A5-BB9A-686548C482F8";

    public static readonly Guid GuidVsChromiumCmdSet = new Guid(GuidVsChromiumCmdSetString);
    public static readonly Guid GuidCodeSearchToolWindow = new Guid(GuidCodeSearchToolWindowString);
    public static readonly Guid GuidBuildExplorerToolWindow = new Guid(GuidBuildExplorerToolWindowString);
  };
}
