// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.IndexServerInfo {
  public interface IShowServerInfoService {
    void ShowServerStatusDialog();
    void ShowProjectIndexDetailsDialog(string path);
    void ShowDirectoryIndexDetailsDialog(string path);

    void FetchDatabaseStatistics(bool forceGarbageCollect, Action<GetDatabaseStatisticsResponse> callback);

    string GetIndexStatusText(GetDatabaseStatisticsResponse response);
    string GetIndexingServerStatusText(GetDatabaseStatisticsResponse response);
    string GetIndexingServerStatusToolTipText(GetDatabaseStatisticsResponse response);
  }
}