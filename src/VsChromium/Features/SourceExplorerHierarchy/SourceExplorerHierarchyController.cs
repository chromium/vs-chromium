// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Commands;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.ServerProxy;
using VsChromium.Threads;
using VsChromium.Views;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class SourceExplorerHierarchyController : ISourceExplorerHierarchyController {
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IFileSystemTreeSource _fileSystemTreeSource;
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IVsGlyphService _vsGlyphService;
    private readonly IOpenDocumentHelper _openDocumentHelper;
    private readonly IFileSystem _fileSystem;
    private readonly IClipboard _clipboard;
    private readonly IWindowsExplorer _windowsExplorer;
    private readonly VsHierarchy _hierarchy;

    public SourceExplorerHierarchyController(
      ISynchronizationContextProvider synchronizationContextProvider,
      IFileSystemTreeSource fileSystemTreeSource,
      IVisualStudioPackageProvider visualStudioPackageProvider,
      IVsGlyphService vsGlyphService,
      IOpenDocumentHelper openDocumentHelper,
      IFileSystem fileSystem,
      IClipboard clipboard,
      IWindowsExplorer windowsExplorer) {

      _synchronizationContextProvider = synchronizationContextProvider;
      _fileSystemTreeSource = fileSystemTreeSource;
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _vsGlyphService = vsGlyphService;
      _openDocumentHelper = openDocumentHelper;
      _fileSystem = fileSystem;
      _clipboard = clipboard;
      _windowsExplorer = windowsExplorer;
      _hierarchy = new VsHierarchy(
        visualStudioPackageProvider.Package.ServiceProvider,
        vsGlyphService);
    }

    public void Activate() {
      IVsSolutionEventsHandler vsSolutionEvents = new VsSolutionEventsHandler(_visualStudioPackageProvider);
      vsSolutionEvents.AfterOpenSolution += AfterOpenSolutionHandler;
      vsSolutionEvents.BeforeCloseSolution += BeforeCloseSolutionHandler;

      _fileSystemTreeSource.TreeReceived += OnTreeReceived;
      _fileSystemTreeSource.ErrorReceived += OnErrorReceived;

      // Force getting the tree and refreshing the ui hierarchy.
      _fileSystemTreeSource.Fetch();

      var mcs = _visualStudioPackageProvider.Package.OleMenuCommandService;
      if (mcs != null) {
        var cmd = new SimplePackageCommandHandler(
          new CommandID(GuidList.GuidVsChromiumCmdSet, (int) PkgCmdIdList.CmdidSyncToDocument),
          () => _hierarchy.Nodes.RootNode.GetChildrenCount() >= 1,
          (s, e) => SyncToActiveDocument());
        mcs.AddCommand(cmd.ToOleMenuCommand());
      }

      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VsUIHierarchyWindowCmds, (int)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_DoubleClick),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => OpenDocument(args.Node.Path)
      });

      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VsUIHierarchyWindowCmds, (int)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_EnterKey),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => OpenDocument(args.Node.Path)
      });

      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VsUIHierarchyWindowCmds, (int)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_RightClick),
        IsEnabled = node => true,
        Execute = args => ShowContextMenu(args.Node, args.VariantIn)
      });

      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.Open),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => OpenDocument(args.Node.Path)
      });

      // TODO(rpaquay): Implement "Open With..." behavior.
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.OpenWith),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => OpenDocument(args.Node.Path)
      });

      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFullPath),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _clipboard.SetText(args.Node.Path)
      });
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFullPathPosix),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _clipboard.SetText(PathHelpers.ToPosix(args.Node.Path))
      });
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyRelativePath),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _clipboard.SetText(args.Node.GetRelativePath())
      });
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyRelativePathPosix),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _clipboard.SetText(PathHelpers.ToPosix(args.Node.GetRelativePath()))
      });
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidOpenFolderInExplorer),
        IsEnabled = node => node is DirectoryNodeViewModel,
        Execute = args => _windowsExplorer.OpenFolder(args.Node.Path)
      });

      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFileFullPath),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _clipboard.SetText(args.Node.Path)
      });
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFileFullPathPosix),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _clipboard.SetText(PathHelpers.ToPosix(args.Node.Path))
      });
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFileRelativePath),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _clipboard.SetText(args.Node.GetRelativePath())
      });
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCopyFileRelativePathPosix),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _clipboard.SetText(PathHelpers.ToPosix(args.Node.GetRelativePath()))
      });
      _hierarchy.AddCommandHandler(new VsHierarchyCommandHandler {
        CommandId = new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidOpenContainingFolder),
        IsEnabled = node => node is FileNodeViewModel,
        Execute = args => _windowsExplorer.OpenContainingFolder(args.Node.Path)
      });
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
        Logger.LogError("Error showing context menu.");
        return;
      }
    }

    private void OpenDocument(string path) {
      Logger.WrapActionInvocation(
        () => {
          if (!_fileSystem.FileExists(new FullPath(path)))
            return;
          _openDocumentHelper.OpenDocument(path, view => null);
        });
    }

    private void SyncToActiveDocument() {
      Logger.WrapActionInvocation(
        () => {
          var dte = _visualStudioPackageProvider.Package.DTE;
          var document = dte.ActiveDocument;
          if (document == null)
            return;
          var path = document.FullName;
          if (!PathHelpers.IsAbsolutePath(path))
            return;
          if (!PathHelpers.IsValidBclPath(path))
            return;

          // TODO(rpaquay): Make this more efficient?
          NodeViewModel node;
          if (_hierarchy.Nodes.RootNode.FindNodeByMoniker(path, out node)) {
            _hierarchy.SelectNode(node);
          }
        });
    }

    /// <summary>
    /// Note: This is executed on the UI thread.
    /// </summary>
    private void AfterOpenSolutionHandler() {
      _hierarchy.Refresh();
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
      var nodes = new VsHierarchyNodes();
      SetupRootNode(nodes.RootNode);
      if (fileSystemTree != null) {
        foreach (var root in fileSystemTree.Root.Entries) {
          AddRootEntry(nodes, root);
        }
      }

      _synchronizationContextProvider.UIContext.Post(
        () => _hierarchy.SetNodes(nodes));
    }

    private void SetupRootNode(NodeViewModel root) {
      var name = "VS Chromium Projects";
      root.Name = name;
      root.Caption = name;
      root.ExpandByDefault = true;
      root.ImageIndex = _vsGlyphService.GetImageIndex(
        StandardGlyphGroup.GlyphClosedFolder,
        StandardGlyphItem.GlyphItemPublic);
      root.OpenFolderImageIndex = _vsGlyphService.GetImageIndex(
        StandardGlyphGroup.GlyphOpenFolder,
        StandardGlyphItem.GlyphItemPublic);
    }

    private void AddRootEntry(VsHierarchyNodes nodes, FileSystemEntry root) {
      AddNodeForEntry(nodes, root, null);
    }

    private void AddNodeForEntry(VsHierarchyNodes nodes, FileSystemEntry entry, NodeViewModel parent) {
      var directoryEntry = entry as DirectoryEntry;
      var node = directoryEntry != null
        ? (NodeViewModel)new DirectoryNodeViewModel()
        : (NodeViewModel)new FileNodeViewModel();

      node.Caption = entry.Name;
      node.Name = entry.Name;
      node.Path =
        (parent == null ? entry.Name : PathHelpers.CombinePaths(parent.Path, entry.Name));
      node.ExpandByDefault = (parent == null);
      node.IsExpanded = (parent == null);
      if (directoryEntry != null) {
        node.ImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphClosedFolder,
          StandardGlyphItem.GlyphItemPublic);
        node.OpenFolderImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphOpenFolder,
          StandardGlyphItem.GlyphItemPublic);

        foreach (var child in directoryEntry.Entries) {
          AddNodeForEntry(nodes, child, node);
        }
      } else {
        node.ImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphCSharpFile,
          StandardGlyphItem.GlyphItemPublic);
      }

      nodes.AddNode(node, parent ?? nodes.RootNode);
    }

    private void OnErrorReceived(ErrorResponse errorResponse) {
      // TODO(rpaquay)
    }
  }
}