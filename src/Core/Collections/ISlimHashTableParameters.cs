// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Collections {
  public interface ISlimHashTableParameters<TKey, TValue> {
    Func<TValue, TKey> KeyGetter { get; }
    Action Locker { get; }
    Action Unlnlocker { get; }
  }
}