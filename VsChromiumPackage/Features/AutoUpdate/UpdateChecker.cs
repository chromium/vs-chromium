using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromiumCore;
using VsChromiumCore.Linq;
using VsChromiumPackage.Threads;

namespace VsChromiumPackage.Features.AutoUpdate {
  /// <summary>
  /// Implements new VsChromium package version check by periodically
  /// (once per day) checking for the latest version info.
  /// </summary>
  [Export(typeof(IUpdateChecker))]
  public class UpdateChecker : IUpdateChecker {
    private readonly IPackageVersionProvider _packageVersionProvider;
    private readonly IUpdateInfoProvider _updateInfoProvider;
    private readonly IDelayedOperationProcessor _delayedOperationProcessor;
    private readonly IEnumerable<IUpdateNotificationListener> _updateNotificationListeners;
    private int _sequenceNumber;

    [ImportingConstructor]
    public UpdateChecker(
      IPackageVersionProvider packageVersionProvider,
      IUpdateInfoProvider updateInfoProvider,
      IDelayedOperationProcessor delayedOperationProcessor,
      [ImportMany]IEnumerable<IUpdateNotificationListener> updateNotificationListeners) {
      _packageVersionProvider = packageVersionProvider;
      _updateInfoProvider = updateInfoProvider;
      _delayedOperationProcessor = delayedOperationProcessor;
      _updateNotificationListeners = updateNotificationListeners;
    }

    public void Start() {
      EnqueueOperation();
    }

    private void EnqueueOperation() {
      _sequenceNumber++;
      var operation = new DelayedOperation {
        Id = this.GetType().Name + "-" + _sequenceNumber,
        Delay = GetDelay(),
        Action = RunCheck
      };
      _delayedOperationProcessor.Post(operation);
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
        Logger.LogException(e, "Error checking for latest update information");
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
      Logger.Log("New version {0} available online at {1}", updateInfo.Version, updateInfo.Url);
      _updateNotificationListeners.ForAll(x => x.NotifyUpdate(updateInfo));
    }
  }
}