// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.DkmIntegration {
  class DebugProcessOptions {
    private const string kAutoAttachKey = "AUTO_ATTACH_CHILDREN";

    private enum Option {
      AutoAttach
    }

    private static void GetBoolOption(Dictionary<Option, string> dict, Option key, bool def, out bool value) {
      value = def;
      string str;
      if (dict.TryGetValue(Option.AutoAttach, out str)) {
        bool.TryParse(str, out value);
      }
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
          case kAutoAttachKey:
            dict[Option.AutoAttach] = value;
            break;
        }
      }

      return dict;
    }

    public static DebugProcessOptions Create(string options) {
      bool autoAttachToChildren = false;
      Dictionary<Option, string> dict = ParseOptionString(options);
      GetBoolOption(dict, Option.AutoAttach, false, out autoAttachToChildren);

      return new DebugProcessOptions { AutoAttachToChildren = autoAttachToChildren };
    }

    public string OptionsString {
      get {
        StringBuilder builder = new StringBuilder();
        builder.AppendFormat("AUTO_ATTACH_CHILDREN={0}", AutoAttachToChildren);
        return builder.ToString();
      }
    }

    public bool AutoAttachToChildren { get; set; }
  }
}
