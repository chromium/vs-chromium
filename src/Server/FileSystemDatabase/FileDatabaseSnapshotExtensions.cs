// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
  public static class FileDatabaseSnapshotExtensions {
    public static bool IsContainedInSymLink(this IFileDatabaseSnapshot snapshot, FileName name) {
      return snapshot.IsContainedInSymLink(name.Parent);
    }
  }
}