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

      // Step 3. Set target properties 
      string layout =
        @"[${longdate}][" + id + @"][${processid}-${threadid}] ${message}" +
        @"${onexception:" +
        @"${newline}EXCEPTION-LOG${newline}${exception:format=type,message,stacktrace,Data:maxInnerExceptionLevel=10:separator=\\r\\n}" +
        @"}";

      // One file for all processes in %LOCALAPPDATA%/VsChromium, with up to 10 archives of 2MB each.
      fileTarget.FileName = "${specialfolder:folder=LocalApplicationData}/VsChromium/" + fileName + ".log";
      fileTarget.Layout = layout;
      fileTarget.KeepFileOpen = true; // For performance
      fileTarget.ConcurrentWrites = true;
      fileTarget.ArchiveAboveSize = 2 * 1024 * 1024;  // 2 MB
      fileTarget.ArchiveNumbering = ArchiveNumberingMode.Rolling;
      fileTarget.MaxArchiveFiles = 10;

      // Step 4. Define rules
      var rule = new LoggingRule("*", LogLevel.Info, fileTarget);
      config.LoggingRules.Add(rule);

      // Step 5. Activate the configuration
      LogManager.Configuration = config;
    }
  }
}
