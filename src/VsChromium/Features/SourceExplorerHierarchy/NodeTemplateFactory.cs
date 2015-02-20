// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Language.Intellisense;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class NodeTemplateFactory : INodeTemplateFactory {
    private readonly NodeViewModelTemplate _directoryTemplate;
    private readonly NodeViewModelTemplate _fileTemplate;
    private readonly NodeViewModelTemplate _rootNodeTemplate;
    private readonly NodeViewModelTemplate _projectTemplate;

    public NodeTemplateFactory(IVsGlyphService vsGlyphService) {
      _rootNodeTemplate = new NodeViewModelTemplate {
        ImageIndex = vsGlyphService.GetImageIndex(StandardGlyphGroup.GlyphLibrary, StandardGlyphItem.GlyphItemPublic),
        ExpandByDefault = true
      };

      _projectTemplate = new NodeViewModelTemplate {
        ImageIndex = vsGlyphService.GetImageIndex(StandardGlyphGroup.GlyphClosedFolder, StandardGlyphItem.GlyphItemPublic),
        OpenFolderImageIndex = vsGlyphService.GetImageIndex(StandardGlyphGroup.GlyphOpenFolder, StandardGlyphItem.GlyphItemPublic),
        ExpandByDefault = true
      };

      _directoryTemplate = new NodeViewModelTemplate {
        ImageIndex = vsGlyphService.GetImageIndex(StandardGlyphGroup.GlyphClosedFolder, StandardGlyphItem.GlyphItemPublic),
        OpenFolderImageIndex = vsGlyphService.GetImageIndex(StandardGlyphGroup.GlyphOpenFolder, StandardGlyphItem.GlyphItemPublic)
      };

      _fileTemplate = new NodeViewModelTemplate {
        ImageIndex = vsGlyphService.GetImageIndex(StandardGlyphGroup.GlyphCSharpFile, StandardGlyphItem.GlyphItemPublic)
      };
    }

    public NodeViewModelTemplate RootNodeTemplate { get { return _rootNodeTemplate; } }
    public NodeViewModelTemplate ProjectTemplate { get { return _projectTemplate; } }
    public NodeViewModelTemplate DirectoryTemplate { get { return _directoryTemplate; } }
    public NodeViewModelTemplate FileTemplate { get { return _fileTemplate; } }
  }
}