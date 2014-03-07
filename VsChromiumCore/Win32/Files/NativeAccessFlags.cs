// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VsChromiumCore.Win32.Files {
  [Flags]
  public enum NativeAccessFlags : uint {
    GenericWrite = 0x40000000,
    GenericRead = 0x80000000
  }
}
