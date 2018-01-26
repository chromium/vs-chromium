// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using NLog;
using NLog.Config;
using NLog.Targets;

namespace VsChromium.Core.Logging {
  class NLogConfig {
    public static void ConfigureApplication(string fileName, string id) {

      // Step 1. Create configuration object 
      var config = new LoggingConfiguration();

      // Step 2. Create targets and add them to the configuration 
      var fileTarget = new FileTarget();
      config.AddTarget("file", fileTarget);

      var fileTarget2 = new FileTarget();
      config.AddTarget("file", fileTarget2);

      // Step 3. Set target properties 
      string layout =
        @"[${longdate}]"+
        @"[${pad:fixedLength=True:padding=-10:inner=" + id + @"}]" +
        @"[${pad:fixedLength=True:padding=8:inner=${processid}-${threadid}}]" +
        @"[${pad:fixedLength=True:padding=-5:inner=${level}}] " +
        @"${message}" +
        @"${onexception:" +
        @"${newline}EXCEPTION-LOG${newline}${exception:format=type,message,stacktrace,Data:maxInnerExceptionLevel=10:separator=\\r\\n}" +
        @"}";

      // One file shared by all processes, located in %LOCALAPPDATA%/VsChromium,
      // with up to 10 archives of 2MB each.
      fileTarget.FileName = "${specialfolder:folder=LocalApplicationData}/VsChromium/" + fileName + ".log";
      SetupFileTarget(fileTarget, layout);

      fileTarget2.FileName = "${specialfolder:folder=LocalApplicationData}/VsChromium/" + fileName + ".errors.log";
      SetupFileTarget(fileTarget2, layout);

      // Step 4. Define rules
      var rule = new LoggingRule("*", LogLevel.Info, fileTarget);
      config.LoggingRules.Add(rule);

      var rule2 = new LoggingRule("*", LogLevel.Warn, fileTarget2);
      config.LoggingRules.Add(rule2);

      // Step 5. Activate the configuration
      LogManager.Configuration = config;
    }

    private static void SetupFileTarget(FileTarget fileTarget, string layout) {
      fileTarget.Layout = layout;
      fileTarget.KeepFileOpen = true; // For performance
      fileTarget.ConcurrentWrites = true;
      fileTarget.ArchiveAboveSize = 2 * 1024 * 1024; // 2 MB
      fileTarget.ArchiveNumbering = ArchiveNumberingMode.Rolling;
      fileTarget.MaxArchiveFiles = 10;
    }
  }
}
