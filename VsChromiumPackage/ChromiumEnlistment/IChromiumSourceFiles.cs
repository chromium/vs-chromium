// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.ChromiumEnlistment {
  public interface IChromiumSourceFiles {
    /// <summary>
    /// Returns true if |filename| is a file that should abide to the Chromium Coding Style.
    /// </summary>
    bool ApplyCodingStyle(string filename);

    /// <summary>
    /// Reset internal cache, usually when something drastic happened on the file system.
    /// </summary>
    void ValidateCache();
  }
}
