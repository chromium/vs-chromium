// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using VsChromium.Commands;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Features.IndexServerInfo;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;
using VsChromium.ServerProxy;
using VsChromium.Settings;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class SourceExplorerHierarchyController : ISourceExplorerHierarchyController {
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IFileSystemTreeSource _fileSystemTreeSource;
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IImageSourceFactory _imageSourceFactory;
    private readonly IOpenDocumentHelper _openDocumentHelper;
    private readonly IFileSystem _fileSystem;
    private readonly IClipboard _clipboard;
    private readonly IWindowsExplorer _windowsExplorer;
    private readonly IDispatchThreadServerRequestExecutor _dispatchThreadServerRequestExecutor;
    private readonly IEventBus _eventBus;
    private readonly IGlobalSettingsProvider _globalSettingsProvider;
    private readonly IDelayedOperationExecutor _delayedOperationExecutor;
    private readonly IShowServerInfoService _showServerInfoService;
    private readonly VsHierarchyAggregate _hierarchy;
    private readonly NodeTemplateFactory _nodeTemplateFactory;
    private readonly NodeViewModelLoader _nodeViewModelLoader;

    /// <summary>
    /// Keeps track of the latest file system tree version received from the
    /// server, so that we can ensure only the latest update wins in case of
    /// concurrent updates.
    /// </summary>
    private int _latestFileSystemTreeVersion;

    public SourceExplorerHierarchyController(
      ISynchronizationContextProvider synchronizationContextProvider,
      IFileSystemTreeSource fileSystemTreeSource,
      IVisualStudioPackageProvider visualStudioPackageProvider,
      IVsGlyphService vsGlyphService,
      IImageSourceFactory imageSourceFactory,
      IOpenDocumentHelper openDocumentHelper,
      IFileSystem fileSystem,
      IClipboard clipboard,
      IWindowsExplorer windowsExplorer,
      IDispatchThreadServerRequestExecutor dispatchThreadServerRequestExecutor,
      ITypedRequestProcessProxy typedRequestProcessProxy,
      IEventBus eventBus,
      IGlobalSettingsProvider globalSettingsProvider,
      IDelayedOperationExecutor delayedOperationExecutor,
      IDispatchThread dispatchThread,
      IShowServerInfoService showServerInfoService) {
      _synchronizationContextProvider = synchronizationContextProvider;
      _fileSystemTreeSource = fileSystemTreeSource;
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _imageSourceFactory = imageSourceFactory;
      _openDocumentHelper = openDocumentHelper;
      _fileSystem = fileSystem;
      _clipboard = clipboard;
      _windowsExplorer = windowsExplorer;
      _dispatchThreadServerRequestExecutor = dispatchThreadServerRequestExecutor;
      _eventBus = eventBus;
      _globalSettingsProvider = globalSettingsProvider;
      _delayedOperationExecutor = delayedOperationExecutor;
      _showServerInfoService = showServerInfoService;
      _nodeTemplateFactory = new NodeTemplateFactory(vsGlyphService, imageSourceFactory);
      _nodeViewModelLoader = new NodeViewModelLoader(typedRequestProcessProxy);
      _hierarchy = new VsHierarchyAggregate(
        visualStudioPackageProvider.Package.ServiceProvider,
        vsGlyphService,
        _imageSourceFactory,
        _nodeTemplateFactory,
        _nodeViewModelLoader,
        dispatchThread);
    }

    public void Activate() {
      IVsSolutionEventsHandler vsSolutionEvents = new VsSolutionEventsHandler(_visualStudioPackageProvider);
      vsSolutionEvents.AfterOpenSolution += AfterOpenSolutionHandler;
      vsSolutionEvents.BeforeCloseSolution += BeforeCloseSolutionHandler;

      _fileSystemTreeSource.TreeReceived += OnTreeReceived;
      _fileSystemTreeSource.ErrorReceived += OnErrorReceived;

      var mcs = _visualStudioPackageProvider.Package.OleMenuCommandService;
      if (mcs != null) {
        var cmd = new SimplePackageCommandHandler(
          new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidSyncWithActiveDocument),
          enabled: () => !_hierarchy.IsEmpty,
          visible: () => _globalSettingsProvider.GlobalSettings.EnableSourceExplorerHierarchy && !_hierarchy.IsEmpty,
          execute: (s, e) => SyncToActiveDocument());
        mcs.AddCommand(cmd.ToOleMenuCommand());
      }

      RegisterHierarchyCommands(_hierarchy);

      _nodeTemplateFactory.Activate();
      _eventBus.RegisterHandler("ShowInSolutionExplorer", ShowInSolutionExplorerHandler);

      _globalSettingsProvider.GlobalSettings.PropertyChanged += GlobalSettingsOnPropertyChanged;
      SynchronizeHierarchy();
    }

    private void RegisterHierarchyCommands(IVsHierarchyImpl hierarchy) {
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VsUIHierarchyWindowCmds, (int)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_DoubleClick),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => OpenDocument(args.Hierarchy, args.Node)
      });

      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VsUIHierarchyWindowCmds, (int)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_EnterKey),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => OpenDocument(args.Hierarchy, args.Node)
      });

      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VsUIHierarchyWindowCmds, (int)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_RightClick),
        IsEnabled = node => true,
        Execute = args => ShowContextMenu(args.Node, args.VariantIn)
      });

      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.Open),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => OpenDocument(args.Hierarchy, args.Node)
      });

      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.OpenWith),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => OpenDocument(args.Hierarchy, args.Node, openWith: true)
      });

      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.VSStd2K, (int)VSConstants.VSStd2KCmdID.SLNREFRESH),
        IsEnabled = node => true,
        Execute = args => RefreshFileSystemTree()
      });

      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFullPath),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _clipboard.SetText(args.Node.FullPathString)
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFullPathPosix),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _clipboard.SetText(PathHelpers.ToPosix(args.Node.FullPathString))
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyRelativePath),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _clipboard.SetText(args.Node.RelativePath)
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyRelativePathPosix),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _clipboard.SetText(PathHelpers.ToPosix(args.Node.RelativePath))
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidOpenFolderInExplorer),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _windowsExplorer.OpenFolder(args.Node.FullPathString)
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidShowProjectIndexDetails),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => ShowProjectIndexDetails(args.Node.FullPathString)
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidShowDirectoryIndexDetails),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => ShowDirectoryIndexDetails(args.Node.FullPathString)
      });


      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFileFullPath),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _clipboard.SetText(args.Node.FullPathString)
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFileFullPathPosix),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _clipboard.SetText(PathHelpers.ToPosix(args.Node.FullPathString))
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFileRelativePath),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _clipboard.SetText(args.Node.RelativePath)
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFileRelativePathPosix),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _clipboard.SetText(PathHelpers.ToPosix(args.Node.RelativePath))
      });
      hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidOpenContainingFolder),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _windowsExplorer.OpenContainingFolder(args.Node.FullPathString)
      });
    }

    private void GlobalSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs args) {
      var name = args.PropertyName;
      var model = (GlobalSettings)sender;
      if (name == ReflectionUtils.GetPropertyName(model, x => x.EnableSourceExplorerHierarchy)) {
        SynchronizeHierarchy();
      }
    }

    private void SynchronizeHierarchy() {
      // Force getting the tree and refreshing the ui hierarchy.
      if (_globalSettingsProvider.GlobalSettings.EnableSourceExplorerHierarchy) {
        _fileSystemTreeSource.Fetch();
      } else {
        _hierarchy.Disable();
      }
    }

    private void ShowInSolutionExplorerHandler(object sender, EventArgs eventArgs) {
      ShowInSolutionExplorer(((FilePathEventArgs)eventArgs).FilePath);
    }

    private void RefreshFileSystemTree() {
      var uiRequest = new DispatchThreadServerRequest {
        Request = new RefreshFileSystemTreeRequest(),
        Id = "RefreshFileSystemTreeRequest",
        Delay = TimeSpan.FromSeconds(0.0),
      };

      _dispatchThreadServerRequestExecutor.Post(uiRequest);
    }

    private void ShowProjectIndexDetails(string path) {
      _showServerInfoService.ShowProjectIndexDetailsDialog(path);
    }

    private void ShowDirectoryIndexDetails(string path) {
      _showServerInfoService.ShowDirectoryIndexDetailsDialog(path);
    }

    private void ShowContextMenu(NodeViewModel node, IntPtr variantIn) {
      // See https://msdn.microsoft.com/en-us/library/microsoft.visualstudio.vsconstants.vsuihierarchywindowcmdids.aspx
      //
      // The UIHWCMDID_RightClick command is what tells the interface
      // IVsUIHierarchy in a IVsUIHierarchyWindow to display the context menu.
      // Since the mouse position may change between the mouse down and the
      // mouse up events and the right click command might even originate from
      // the keyboard Visual Studio provides the proper menu position into
      // pvaIn by performing a memory copy operation on a POINTS structure
      // into the VT_UI4 part of the pvaIn variant.
      //
      // To show the menu use the derived POINTS as the coordinates to show
      // the context menu, calling ShowContextMenu. To ensure proper command
      // handling you should pass a NULL command target into ShowContextMenu
      // menu so that the IVsUIHierarchyWindow will have the first chance to
      // handle commands like delete.
      object variant = Marshal.GetObjectForNativeVariant(variantIn);
      var pointsAsUint = (UInt32)variant;
      var x = (short)(pointsAsUint & 0xffff);
      var y = (short)(pointsAsUint >> 16);
      var points = new POINTS();
      points.x = x;
      points.y = y;

      var shell = _visualStudioPackageProvider.Package.ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
      if (shell == null) {
        Logger.LogError("Error accessing IVsUIShell service.");
        return;
      }

      var pointsIn = new POINTS[1];
      pointsIn[0].x = points.x;
      pointsIn[0].y = points.y;
      var groupGuid = VsMenus.guidSHLMainMenu;
      var menuId = (node.IsRoot)
        ? VsMenus.IDM_VS_CTXT_PROJNODE
        : (node is DirectoryNodeViewModel)
          ? VsMenus.IDM_VS_CTXT_FOLDERNODE
          : VsMenus.IDM_VS_CTXT_ITEMNODE;
      int hresult = shell.ShowContextMenu(0, ref groupGuid, menuId, pointsIn, null);
      if (!ErrorHandler.Succeeded(hresult)) {
        Logger.LogHResult(hresult, "Error showing context menu.");
      }
    }

    private void OpenDocument(VsHierarchy hierarchy, NodeViewModel node, bool openWith = false) {
      Logger.WrapActionInvocation(
        () => {
          if (!_fileSystem.FileExists(new FullPath(node.FullPathString)))
            return;
          if (openWith)
            _openDocumentHelper.OpenDocumentWith(node.FullPathString, hierarchy, node.ItemId, view => null);
          else
            _openDocumentHelper.OpenDocument(node.FullPathString, view => null);
        });
    }

    private void SyncToActiveDocument() {
      Logger.WrapActionInvocation(
        () => {
          var dte = _visualStudioPackageProvider.Package.DTE;
          var document = dte.ActiveDocument;
          if (document == null)
            return;
          ShowInSolutionExplorer(document.FullName);
        });
    }

    private void ShowInSolutionExplorer(string path) {
      if (!PathHelpers.IsAbsolutePath(path))
        return;
      if (!PathHelpers.IsValidBclPath(path))
        return;

      _hierarchy.SelectNodeByFilePath(path);
    }

    /// <summary>
    /// Note: This is executed on the UI thread.
    /// </summary>
    private void AfterOpenSolutionHandler() {
      _hierarchy.Reconnect();
    }

    /// <summary>
    /// Note: This is executed on the UI thread.
    /// </summary>
    private void BeforeCloseSolutionHandler() {
      _hierarchy.Disconnect();
    }

    /// <summary>
    /// Note: This is executed on a background thred.
    /// </summary>
    private void OnTreeReceived(FileSystemTree fileSystemTree) {
      if (!_globalSettingsProvider.GlobalSettings.EnableSourceExplorerHierarchy)
        return;

      _latestFileSystemTreeVersion = fileSystemTree.Version;
      PostApplyFileSystemTreeToVsHierarchy(fileSystemTree);
    }

    private void PostApplyFileSystemTreeToVsHierarchy(FileSystemTree fileSystemTree) {
      _delayedOperationExecutor.Post(
        new DelayedOperation {
          Id = "ApplyFileSystemTreeToVsHierarchy",
          Action = () => ApplyFileSystemTreeToVsHierarchy(fileSystemTree),
          Delay = TimeSpan.FromSeconds(0.1),
        });
    }

    private void ApplyFileSystemTreeToVsHierarchy(FileSystemTree fileSystemTree) {
      var builder = CreateIncrementalBuilder(fileSystemTree);
      var applyChanges = builder.ComputeChangeApplier();

      _synchronizationContextProvider.DispatchThreadContext.Post(() => {
        var result = applyChanges(_latestFileSystemTreeVersion);
        if (result == ApplyChangesResult.Retry) {
          PostApplyFileSystemTreeToVsHierarchy(fileSystemTree);
        }
      });
    }

    private IIncrementalHierarchyBuilder CreateIncrementalBuilder(FileSystemTree fileSystemTree) {
      return new IncrementalHierarchyBuilderAggregate(
        _nodeTemplateFactory,
        _hierarchy,
        fileSystemTree,
        _nodeViewModelLoader,
        _imageSourceFactory);
    }


    private void OnErrorReceived(ErrorResponse errorResponse) {
      // TODO(rpaquay)
    }
  }
}