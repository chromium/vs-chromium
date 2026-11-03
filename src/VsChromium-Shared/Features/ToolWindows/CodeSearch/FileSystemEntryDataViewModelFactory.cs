// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Protocol.Generated;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public static class FileSystemEntryDataViewModelFactory {
    public static IEnumerable<TreeViewItemViewModel> CreateViewModels(ICodeSearchController host, FileEntryViewModel parent, IList<TextSpan> spans) {
      if (spans != null)
        return spans.Select(x => new FilePositionViewModel(host, parent, x));

      return Enumerable.Empty<TreeViewItemViewModel>();
    }
  }
}
