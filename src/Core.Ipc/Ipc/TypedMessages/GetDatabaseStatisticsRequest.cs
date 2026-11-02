// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class GetDatabaseStatisticsRequest : TypedRequest {
    public bool ForceGabageCollection { get; set; }
  }
}
