﻿// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.Threads;

namespace VsChromium.Features.AutoUpdate {
  /// <summary>
  /// Implements new VsChromium package version check by periodically
  /// (once per day) checking for the latest version info.
  /// </summary>
  [Export(typeof(IPackagePostInitializer))]
  public class UpdateChecker : IPackagePostInitializer {
    private readonly IPackageVersionProvider _packageVersionProvider;
    private readonly IUpdateInfoProvider _updateInfoProvider;
    private readonly IDelayedOperationExecutor _delayedOperationExecutor;
    private readonly IEnumerable<IUpdateNotificationListener> _updateNotificationListeners;
    private int _sequenceNumber;

    [ImportingConstructor]
    public UpdateChecker(
      IPackageVersionProvider packageVersionProvider,
      IUpdateInfoProvider updateInfoProvider,
      IDelayedOperationExecutor delayedOperationExecutor,
      [ImportMany]IEnumerable<IUpdateNotificationListener> updateNotificationListeners) {
      _packageVersionProvider = packageVersionProvider;
      _updateInfoProvider = updateInfoProvider;
      _delayedOperationExecutor = delayedOperationExecutor;
      _updateNotificationListeners = updateNotificationListeners;
    }

    public int Priority { get { return -100; } }

    public void Run(IVisualStudioPackage package) {
      EnqueueOperation();
    }

    private void EnqueueOperation() {
      _sequenceNumber++;
      var operation = new DelayedOperation {
        Id = this.GetType().Name + "-" + _sequenceNumber,
        Delay = GetDelay(),
        Action = RunCheck
      };
      _delayedOperationExecutor.Post(operation);
    }

    private TimeSpan GetDelay() {
      if (_sequenceNumber == 1)
        return TimeSpan.FromSeconds(60);
      else
        return TimeSpan.FromDays(1);
    }

    private void RunCheck() {
      try {
        PerformVersionCheck();
      }
      catch (Exception e) {
        Logger.LogError(e, "Error checking for latest update information");
      }
      EnqueueOperation();
    }

    private void PerformVersionCheck() {
      var updateInfo = _updateInfoProvider.GetUpdateInfo();
      var currentVersion = _packageVersionProvider.GetVersion();
      if (updateInfo.Version > currentVersion) {
        NotifyUpdate(updateInfo);
      }
    }

    private void NotifyUpdate(UpdateInfo updateInfo) {
      Logger.LogInfo("New version {0} available online at {1}", updateInfo.Version, updateInfo.Url);
      _updateNotificationListeners.ForAll(x => x.NotifyUpdate(updateInfo));
    }
  }
}