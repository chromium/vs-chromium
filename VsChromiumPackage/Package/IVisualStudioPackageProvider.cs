// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumPackage.Package {
  public interface IVisualStudioPackageProvider {
    void Intialize(IVisualStudioPackage package);
    IVisualStudioPackage Package { get; }
  }
}
