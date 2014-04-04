// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.DkmIntegration.ServerComponent {
  // Values from the CV_HREG_e enumeration in cvconst.h
  enum CpuRegister {
    Eax = 17,
    Ecx = 18,
    Edx = 19,
    Ebx = 20,
    Esp = 21,
    Ebp = 22,
    Esi = 23,
    Edi = 24,
    Eip = 33,
  }
}
