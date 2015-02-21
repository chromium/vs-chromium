// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Package {
  public interface IEventBus {
    object RegisterHandler(string eventName, EventHandler handler);
    void UnregisterHandler(object cookie);

    void Fire(string eventName, object sender, EventArgs args);
  }
}