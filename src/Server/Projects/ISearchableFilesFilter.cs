// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Server.Projects {
  public interface ISearchableFilesFilter {
    bool Include(RelativePath fileName);
  }
}
