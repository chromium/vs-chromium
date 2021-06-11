// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Linq;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyAggregate : IVsHierarchyImpl {
    private readonly IServiceProvider _serviceProvider;
    private readonly IVsGlyphService _vsGlyphService;
    private readonly IImageSourceFactory _imageSourceFactory;
    private readonly NodeTemplateFactory _nodeTemplateFactory;
    private readonly INodeViewModelLoader _nodeViewModelLoader;
    private readonly IDispatchThread _dispatchThread;
    private readonly List<VsHierarchyCommandHandler> _commandHandlers = new List<VsHierarchyCommandHandler>();
    private readonly List<VsHierarchy> _hierarchies = new List<VsHierarchy>();
    private readonly object _hierarchiesLock = new object();
    private int _version;

    public VsHierarchyAggregate(IServiceProvider serviceProvider,
      IVsGlyphService vsGlyphService,
      IImageSourceFactory imageSourceFactory,
      NodeTemplateFactory nodeTemplateFactory,
      INodeViewModelLoader nodeViewModelLoader,
      IDispatchThread dispatchThread) {
      _serviceProvider = serviceProvider;
      _vsGlyphService = vsGlyphService;
      _imageSourceFactory = imageSourceFactory;
      _nodeTemplateFactory = nodeTemplateFactory;
      _nodeViewModelLoader = nodeViewModelLoader;
      _dispatchThread = dispatchThread;
      _version = 1;
    }

    public int Version {
      get { return _version; }
    }

    public bool IsEmpty {
      get { return _hierarchies.Count == 0; }
    }

    public void AddCommandHandler(VsHierarchyCommandHandler handler) {
      _commandHandlers.Add(handler);
    }

    public void Reconnect() {
      _hierarchies.ForAll(x => x.Reconnect());
    }

    public void Disconnect() {
      _hierarchies.ForAll(x => x.Disconnect());
    }

    public void Disable() {
      _hierarchies.ForAll(x => x.Disconnect());
    }

    public void SelectNodeByFilePath(string filePath) {
      _hierarchies.ForAll(x => x.SelectNodeByFilePath(filePath));
    }

    public List<VsHierarchy> CloneHierarchyList() {
      lock (_hierarchiesLock) {
        return _hierarchies.ToList();
      }
    }

    public void SetNewHierarchies(List<VsHierarchy> vsHierarchies) {
      _version++;
      lock (_hierarchiesLock) {
        _hierarchies.Clear();
        _hierarchies.AddRange(vsHierarchies);
      }
    }

    public VsHierarchy CreateHierarchy() {
      var result = new VsHierarchy(_serviceProvider, _vsGlyphService, _imageSourceFactory, _nodeTemplateFactory,
        _nodeViewModelLoader, _dispatchThread);
      foreach (var handler in _commandHandlers) {
        result.AddCommandHandler(handler);
      }

      return result;
    }
  }
}