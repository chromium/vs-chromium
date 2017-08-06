// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Files;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public interface IFileRegistrationTracker {
    void RegisterFile(FullPath path);
    void UnregisterFile(FullPath path);

    void Refresh();

    event EventHandler<IList<IProject>> FullRescanRequired;
    event EventHandler<IList<IProject>> ProjectListChanged;
  }
}