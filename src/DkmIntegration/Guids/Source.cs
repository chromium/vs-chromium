// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.DkmIntegration.Guids {
  public static class Source {
    public static readonly Guid FunctionTraceEnter = new Guid("2A48C8D1-6D61-4360-A105-6244F3C7B303");
    public static readonly Guid FunctionTraceExit = new Guid("0B6A3423-7861-46F7-B96F-14AFD344820F");
    public static readonly Guid CreateProcessEnter = new Guid("F1721021-3227-4345-AF50-D2E8D2BE996C");
    public static readonly Guid CreateProcessReturn = new Guid("5D1634CC-0EDB-46E3-8AEC-E227C477C562");
  }
}
