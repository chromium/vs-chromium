// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Core.DkmShared {
  public class DebugProcessOptions {
    private const string kChildDebuggingModeKey = "CHILD_DEBUGGING_MODE";

    private enum Option {
      ChildDebuggingMode
    }

    private static bool GetBoolOption(Dictionary<Option, string> dict, Option key, bool def, out bool value) {
      value = def;
      string str;
      if (dict.TryGetValue(Option.ChildDebuggingMode, out str)) {
        bool.TryParse(str, out value);
        return true;
      }
      return false;
    }

    private static bool GetEnumOption<T>(Dictionary<Option, string> dict, Option key, T def, out T value) where T : struct {
      value = def;
      string str;
      if (dict.TryGetValue(Option.ChildDebuggingMode, out str))
        return System.Enum.TryParse<T>(str, out value);
      return false;
    }
    private static Dictionary<Option, string> ParseOptionString(string optionString) {
      Dictionary<Option, string> dict = new Dictionary<Option, string>();

      string[] options = optionString.Split(',');
      foreach (string option in options) {
        int splitIndex = option.IndexOf('=');
        if (splitIndex == -1)
          continue;
        string key = option.Substring(0, splitIndex);
        string value = option.Substring(splitIndex + 1);
        switch (key) {
          case kChildDebuggingModeKey:
            dict[Option.ChildDebuggingMode] = value;
            break;
        }
      }

      return dict;
    }

    public static DebugProcessOptions Create(string options) {
      ChildDebuggingMode childDebuggingMode = ChildDebuggingMode.UseDefault;
      Dictionary<Option, string> dict = ParseOptionString(options);
      bool specified = GetEnumOption<ChildDebuggingMode>(
          dict, Option.ChildDebuggingMode, 
          ChildDebuggingMode.UseDefault, 
          out childDebuggingMode);

      return new DebugProcessOptions { ChildDebuggingMode = childDebuggingMode };
    }

    public string OptionsString {
      get {
        return String.Format("{0}={1}", kChildDebuggingModeKey, ChildDebuggingMode);
      }
    }

    public ChildDebuggingMode ChildDebuggingMode { get; set; }
  }
}
