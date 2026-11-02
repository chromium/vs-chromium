// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Configuration {
  /// <summary>
  /// Name of files/sections supported by both an implicit Chromium enlistment
  /// and an on-disk project file.
  /// </summary>
  public static class ConfigurationSectionNames {
    public static readonly string SourceExplorerIgnore = "SourceExplorer.ignore";
    public static readonly string SearchableFilesIgnore = "SearchableFiles.ignore";
    public static readonly string SearchableFilesInclude = "SearchableFiles.include";
    public static readonly string SourceExplorerIgnoreObsolete = "ChromiumExplorer.ignore";
  }
}