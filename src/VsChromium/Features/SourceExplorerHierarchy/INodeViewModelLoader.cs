// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Threading.Tasks;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public interface INodeViewModelLoader {
    /// <summary>
    /// Returns a <see cref="Task{DirectoryEntry}"/> that completes after 
    /// retrieving the <see cref="DirectoryEntry"/> corresponding to
    /// <paramref name="parentNode"/>
    /// </summary>
    /// <param name="parentNode"></param>
    Task<DirectoryEntry> LoadChildrenAsync(DirectoryNodeViewModel parentNode);
  }
}