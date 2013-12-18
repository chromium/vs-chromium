// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Design;

namespace VsChromiumPackage.Commands {
  /// <summary>
  /// A cleaner interface for IOleCommandTarget.
  /// </summary>
  public interface ICommandTarget {
    /// <summary>
    /// Returns true if commandId is handled. Return false otherwise. IsEnabled and Execute are called
    /// only if HandlesCommand returns true.
    /// </summary>
    bool HandlesCommand(CommandID commandId);
    /// <summary>
    /// Return true if commandId is enabled (e.g. OLECMDF.OLECMDF_ENABLED).
    /// </summary>
    bool IsEnabled(CommandID commandId);
    /// <summary>
    /// Callback action for commandId.
    /// </summary>
    void Execute(CommandID commandId);
  }
}
