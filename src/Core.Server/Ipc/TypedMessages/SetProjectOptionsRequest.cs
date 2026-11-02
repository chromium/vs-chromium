// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Ipc.TypedMessages {
  /// <summary>
  /// Changes the indexing options of a project, at any time (response: <see cref="DoneResponse"/>).
  /// Options are kept by the server for the lifetime of the server process (clients send them
  /// again after starting a server). Null values leave the corresponding option unchanged.
  /// </summary>
  public class SetProjectOptionsRequest : TypedRequest {
    /// <summary>
    /// The root path of the project, or any path inside the project.
    /// </summary>
    public string ProjectPath { get; set; }

    /// <summary>
    /// Enables or disables the trigram filters of the project (enabled by default): faster code
    /// searches, at the cost of memory (about 5% of the size of the file contents, in the managed
    /// heap). When disabled, the filters of the project are released.
    /// </summary>
    public bool? TrigramFiltersEnabled { get; set; }
  }
}
