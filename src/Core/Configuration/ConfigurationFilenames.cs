// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Configuration {
  public static class ConfigurationFilenames {
    public static readonly string ChromiumEnlistmentDetectionPatterns = "ChromiumEnlistmentDetection.patterns";
    public static readonly string ProjectFileNameDetection = "project.vs-chromium-project";
  }

  public static class ConfigurationStyleFilenames {
    public static readonly string ChromiumCodingStyleIgnore = "ChromiumCodingStyle.ignore";
    public static readonly string ChromiumStyleCheckersDisabled = "ChromiumStyleCheckers.disabled";
  }

  /// <summary>
  /// Name of files/sections supported by both an implicit Chromium enlistment and an on-disk
  /// project file.
  /// </summary>
  public static class ConfigurationSectionNames {
    public static readonly string SourceExplorerIgnore = "ChromiumExplorer.ignore";
    public static readonly string SearchableFilesIgnore = "SearchableFiles.ignore";
    public static readonly string SearchableFilesInclude = "SearchableFiles.include";
  }
}
