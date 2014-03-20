// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Core.Chromium {
  public interface IChromiumDiscoveryWithCache<T> {
    T GetEnlistmentRootFromRootpath(FullPathName root, Func<FullPathName, T> factory);
    T GetEnlistmentRootFromFilename(FullPathName filename, Func<FullPathName, T> factory);
    void ValidateCache();
  }
}
