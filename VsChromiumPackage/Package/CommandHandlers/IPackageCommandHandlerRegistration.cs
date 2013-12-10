// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumPackage.Package.CommandHandlers {
  /// <summary>
  /// Handles registration of global (i.e. package) command handlers.
  /// </summary>
  public interface IPackageCommandHandlerRegistration {
    void RegisterCommandHandlers();
  }
}
