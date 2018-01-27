// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Collections {
  public partial class SlimDictionary<TKey, TValue> {
    private class Parameters : ISlimHashTableParameters<TKey, Entry> {
      public Func<Entry, TKey> KeyGetter {
        get { return t => t.Key; }
      }

      public Action Locker {
        get { return () => { }; }
      }

      public Action Unlnlocker {
        get { return () => { }; }
      }
    }
  }
}