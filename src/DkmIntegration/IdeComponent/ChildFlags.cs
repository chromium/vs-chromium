// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger.Evaluation;

namespace VsChromium.DkmIntegration.IdeComponent
{
  [Flags]
  public enum ChildDisplayFlags
  {
    HasCustomFields = 0x1,
    HasDefaultFields = 0x2,
    DefaultFieldsInline = 0x4
  }

  public enum ChildDisplayMode
  {
    Nested,
    Inline
  }
}
