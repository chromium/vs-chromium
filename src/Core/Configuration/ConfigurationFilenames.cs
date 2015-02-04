// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Configuration {
  public static class ConfigurationFileNames {
    /// <summary>
    /// Name of file containing list of path patterns used to detect Chromium enlistements
    /// </summary>
    public static readonly string ChromiumEnlistmentDetectionPatterns = "ChromiumEnlistmentDetection.patterns";
    /// <summary>
    /// Name of the file used to define custom (i.e. non-Chromium) enlistements.
    /// </summary>
    public static readonly string ProjectFileNameObsolete = "project.vs-chromium-project";
    /// <summary>
    /// Name of the file used to define custom (i.e. non-Chromium) enlistements.
    /// </summary>
    public static readonly string ProjectFileName = "vs-chromium-project.txt";
  }
}
