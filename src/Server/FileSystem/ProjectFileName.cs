// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public struct ProjectFileName {
    private readonly IProject _project;
    private readonly FileName _fileName;

    public ProjectFileName(IProject project, FileName fileName) {
      _project = project;
      _fileName = fileName;
    }

    public IProject Project {
      get { return _project; }
    }

    public FileName FileName {
      get { return _fileName; }
    }

    public bool IsNull {
      get { return _project == null; }
    }
  }
}