// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.Language.Intellisense;
using VsChromium.Core.Files;
using VsChromium.Views;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class NodeTemplateFactory : INodeTemplateFactory {
    private readonly IVsGlyphService _vsGlyphService;
    private readonly IImageSourceFactory _imageSourceFactory;
    private readonly Lazy<NodeViewModelTemplate> _rootNodeTemplate;
    private readonly Lazy<NodeViewModelTemplate> _projectTemplate;
    private readonly Lazy<NodeViewModelTemplate> _directoryTemplate;
    private readonly ConcurrentDictionary<string, NodeViewModelTemplate> _fileExtenionTemplates =
      new ConcurrentDictionary<string, NodeViewModelTemplate>(SystemPathComparer.Instance.StringComparer);

    public NodeTemplateFactory(IVsGlyphService vsGlyphService, IImageSourceFactory imageSourceFactory) {
      _vsGlyphService = vsGlyphService;
      _imageSourceFactory = imageSourceFactory;
      _rootNodeTemplate = new Lazy<NodeViewModelTemplate>(CreateRootNodeTemplate);
      _projectTemplate = new Lazy<NodeViewModelTemplate>(CreateProjectTemplate);
      _directoryTemplate = new Lazy<NodeViewModelTemplate>(CreateDirectoryTemplate);
    }

    private NodeViewModelTemplate CreateRootNodeTemplate() {
      return new NodeViewModelTemplate {
        Icon = _imageSourceFactory.GetIcon("VsChromiumIcon"),
        ExpandByDefault = true
      };
    }

    private NodeViewModelTemplate CreateProjectTemplate() {
      return new NodeViewModelTemplate {
        Icon = _imageSourceFactory.GetIcon("ProjectNodeIcon"),
        ExpandByDefault = true
      };
    }

    private NodeViewModelTemplate CreateDirectoryTemplate() {
      return new NodeViewModelTemplate {
        ImageIndex = _vsGlyphService.GetImageIndex(StandardGlyphGroup.GlyphClosedFolder, StandardGlyphItem.GlyphItemPublic),
        OpenFolderImageIndex = _vsGlyphService.GetImageIndex(StandardGlyphGroup.GlyphOpenFolder, StandardGlyphItem.GlyphItemPublic)
      };
    }

    public NodeViewModelTemplate RootNodeTemplate { get { return _rootNodeTemplate.Value; } }
    public NodeViewModelTemplate ProjectTemplate { get { return _projectTemplate.Value; } }
    public NodeViewModelTemplate DirectoryTemplate { get { return _directoryTemplate.Value; } }

    public NodeViewModelTemplate GetFileTemplate(string fileExtension) {
      return _fileExtenionTemplates.GetOrAdd(fileExtension, 
        key => new NodeViewModelTemplate());
    }

    /// <summary>
    /// Execution on the main thread, so that icons are fetched on the main thread.
    /// </summary>
    public void Activate() {
      var a1 = this.RootNodeTemplate;
      var a2 = this.ProjectTemplate;
      var a3 = this.DirectoryTemplate;
    }
  }
}