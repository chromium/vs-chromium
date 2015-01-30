// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium {
  public static class GlobalSettings {
    public static int MaxTextExtractLength = 80;
    public static int SearchFileNamesMaxResults = 2000;
    public static int SearchDirectoryNamesMaxResults = 2000;
    public static int SearchTextMaxResults = 10000;
    public static int SearchTextExpandMaxResults = 30;
    public static int MaxExpandedTreeViewItemCount = 100;
    public static TimeSpan SearchFileNamesDelay = TimeSpan.FromSeconds(0.1);
    public static TimeSpan SearchDirectoryNamesDelay = TimeSpan.FromSeconds(0.1);
    public static TimeSpan SearchTextDelay = TimeSpan.FromSeconds(0.1);
  }
}