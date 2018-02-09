// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  public class ConfigurationSectionContents : IConfigurationSectionContents {
    private readonly FullPath _containingFilePath;
    private readonly string _name;
    private readonly IList<string> _contents;

    public static IConfigurationSectionContents Create(IConfigurationSectionProvider provider, string sectionName) {
      var lines = provider.GetSection(sectionName, FilterDirectories);
      return lines;
    }

    private static IEnumerable<string> FilterDirectories(IEnumerable<string> arg) {
      // Note: The file is user input, so we need to replace "/" with "\".
      return arg
        .Where(line => !IsCommentLine(line))
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => line.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
    }

    private static bool IsCommentLine(string x) {
      return x.TrimStart().StartsWith("#");
    }

    public ConfigurationSectionContents(FullPath containingFilePath, string name, IList<string> contents) {
      // TODO(rpaquay): Find way to invalidate cache.
      _containingFilePath = containingFilePath;
      _name = name;
      _contents = contents;
    }

    public string Name { get { return _name; } }
    public IList<string> Contents { get { return _contents; } }

    public FullPath ContainingFilePath {
      get { return _containingFilePath; }
    }
  }


}