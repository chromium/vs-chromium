// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Protocol.Generated;

namespace VsChromium.Features.IndexServerInfo {
  public interface IShowServerInfoService {
    void ShowServerStatusDialog(bool forceGarbageCollection);
    void ShowProjectIndexDetailsDialog(string path);
    void ShowDirectoryIndexDetailsDialog(string path);

    void FetchDatabaseStatistics(Action<IndexStatistics> callback);

    string GetIndexStatusText(IndexStatistics response);
    string GetIndexingServerStatusText(IndexStatistics response);
    string GetIndexingServerStatusToolTipText(IndexStatistics response);
  }
}
