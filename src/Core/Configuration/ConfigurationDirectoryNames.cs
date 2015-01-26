// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Configuration {
  public static class ConfigurationDirectoryNames {
    /// <summary>
    /// Name of the directory containing default configuration files, where the
    /// vsix package is installed.
    /// </summary>
    public const string LocalInstallConfigurationDirectoryName = "Configuration";
    /// <summary>
    /// Name of the directory containing user-specific configuration files.
    /// </summary>
    public const string LocalUserConfigurationDirectoryName = ".VsChromium";
  }
}