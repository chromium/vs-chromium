// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;

namespace VsChromium.Core.Chromium {
  public interface IChromiumDiscoveryWithCache<T> {
    T GetEnlistmentRootFromRootpath(FullPath root, Func<FullPath, T> factory);
    T GetEnlistmentRootFromFilename(FullPath filename, Func<FullPath, T> factory);
    void ValidateCache();
  }
}
