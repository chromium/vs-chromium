// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class ProgressReportEvent : TypedEvent {
    /// <summary>
    /// Display text corresponding to this progress report event.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Number of completed steps. |Completed| is always smaller than or equal to Total.
    /// If |Completed| equal to |Total|, the operation is complete.
    /// </summary>
    public int Completed { get; set; }

    /// <summary>
    /// Total expected number of steps. If the value is |int.MaxValue|, the total number
    /// of steps is unknown (i.e. indeterminate progress)
    /// </summary>
    public int Total { get; set; }
  }
}
