// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Core.Chromium {
  public interface IChromiumDiscovery {
    FullPath? GetEnlistmentRootPath(FullPath path);
    void ValidateCache();
  }
}
