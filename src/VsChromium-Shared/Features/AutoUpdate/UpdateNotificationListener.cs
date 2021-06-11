// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Features.ToolWindows;
using VsChromium.Threads;

namespace VsChromium.Features.AutoUpdate {
  [Export(typeof(IUpdateNotificationListener))]
  public class UpdateNotificationListener : IUpdateNotificationListener {
    private readonly IToolWindowAccessor _toolWindowAccessor;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;

    [ImportingConstructor]
    public UpdateNotificationListener(IToolWindowAccessor toolWindowAccessor, ISynchronizationContextProvider synchronizationContextProvider) {
      _toolWindowAccessor = toolWindowAccessor;
      _synchronizationContextProvider = synchronizationContextProvider;
    }

    public void NotifyUpdate(UpdateInfo updateInfo) {
      // We have to run this on the UI thread since we are accessing UI components
      _synchronizationContextProvider.DispatchThreadContext.Post(() => {
        _toolWindowAccessor.CodeSearch.NotifyPackageUpdate(updateInfo);
      });
    }
  }
}