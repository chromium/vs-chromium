// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using VsChromium.Core.Configuration;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Features.AutoUpdate;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class SourceExplorerViewModel : ChromiumExplorerViewModelBase {
    private List<TreeViewItemViewModel> _fileSystemTreeNodes = new List<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _directoryNameSearchResultNodes = new List<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _textSearchResultNodes = new List<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _fileNameSearchResultNodes = new List<TreeViewItemViewModel>();
    private ISourceExplorerController _controller;
    private UpdateInfo _updateInfo;

    public enum DisplayKind {
      FileSystemTree,
      FileNameSearchResult,
      DirectoryNameSearchResult,
      TextSearchResult,
    }

    public SourceExplorerViewModel() {
      // Default values for options in toolbar.
      this.IncludeSymLinks = true;
      this.UseRe2Regex = true;
    }

    /// <summary>
    /// Assign the controller associated to this ViewModel. This cannot be done in the constructor
    /// due to the way WPF DataContext objects are instantiated.
    /// </summary>
    public void SetController(ISourceExplorerController controller) {
      _controller = controller;
    }

    public DisplayKind ActiveDisplay {
      get {
        if (ReferenceEquals(ActiveRootNodes, _textSearchResultNodes))
          return DisplayKind.TextSearchResult;
        if (ReferenceEquals(ActiveRootNodes, _fileNameSearchResultNodes))
          return DisplayKind.FileNameSearchResult;
        if (ReferenceEquals(ActiveRootNodes, _directoryNameSearchResultNodes))
          return DisplayKind.DirectoryNameSearchResult;
        return DisplayKind.FileSystemTree;
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool MatchCase { get; set; }

    /// <summary>
    /// Databound!
    /// </summary>
    public string MatchCaseToolTip {
      get {
        return string.Format(
          "Toggle case matching for all searches. " + 
          "Searches are currently case {0}.",
          MatchCase ? "sensitive" : "insensitive");
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool UseRegex { get; set; }

    /// <summary>
    /// Databound!
    /// </summary>
    public string UseRegexToolTip {
      get {
        return string.Format(
          "Toggle usage of regular expressions for all searches. " +
          "Regular expressions are currently {0}.",
          UseRegex ? "enabled" : "disabled");
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool IncludeSymLinks { get; set; }

    /// <summary>
    /// Databound!
    /// </summary>
    public string IncludeSymLinksToolTip {
      get {
        return string.Format(
          "Toggle searching inside symbolic links for all searches. " +
          "Symbolic links are currently {0} in search results.",
          IncludeSymLinks ? "included" : "excluded");
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool UseRe2Regex { get; set; }

    /// <summary>
    /// Databound!
    /// </summary>
    public string UseRe2RegexToolTip {
      get {
        return string.Format(
          "Toggle usage of the RE2 regular expression engine as a replacement of the standard C++ library for improved performance. " +
          "The RE2 engine is currently {0}.",
          UseRe2Regex ? "enabled" : "disabled");
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool EnableChildDebugging { get; set; }

    /// <summary>
    /// Databound!
    /// </summary>
    public ImageSource LightningBoltImage {
      get {
        if (_controller == null)
          return null;
        return _controller.StandarImageSourceFactory.LightningBolt;
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string UpdateInfoText {
      get {
        if (_updateInfo == null)
          return "";
        return string.Format("A new version ({0}) of VsChromium is available: ", _updateInfo.Version);
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string UpdateInfoUrl {
      get {
        if (_updateInfo == null)
          return "";
        return _updateInfo.Url.ToString();
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public Visibility UpdateInfoVisibility {
      get {
        if (_updateInfo == null)
          return Visibility.Collapsed;
        return Visibility.Visible;
      }
    }

    public UpdateInfo UpdateInfo {
      get {
        return _updateInfo;
      }
      set {
        _updateInfo = value;
        OnPropertyChanged("UpdateInfoText");
        OnPropertyChanged("UpdateInfoUrl");
        OnPropertyChanged("UpdateInfoVisibility");
      }
    }

    /// <summary>
    /// The root nodes representing the file system tree from the server.
    /// </summary>
    public List<TreeViewItemViewModel> FileSystemTreeNodes {
      get { return _fileSystemTreeNodes; }
    }

    public void SwitchToFileSystemTree() {
      var defaultMessage = string.Format(
        "Open a source file from a local Chromium enlistment " + 
        "or a directory containing a \"{0}\" file.",
        ConfigurationFilenames.ProjectFileNameDetection);
      SetRootNodes(FileSystemTreeNodes, defaultMessage);
    }

    private void SwitchToFileNamesSearchResult() {
      SetRootNodes(_fileNameSearchResultNodes);
    }

    private void SwitchToDirectoryNamesSearchResult() {
      SetRootNodes(_directoryNameSearchResultNodes);
    }

    private void SwitchToTextSearchResult() {
      SetRootNodes(_textSearchResultNodes);
    }

    public void SetFileSystemTree(FileSystemTree tree) {
      var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
      _fileSystemTreeNodes = new List<TreeViewItemViewModel>(tree.Root
        .Entries
        .Select(x => FileSystemEntryViewModel.Create(_controller, rootNode, x)));
      FileSystemTreeNodes.ForAll(x => rootNode.AddChild(x));
      ExpandNodes(FileSystemTreeNodes, false);
      SwitchToFileSystemTree();
    }

    public void SetFileNamesSearchResult(DirectoryEntry fileResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
      _fileNameSearchResultNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(_controller.StandarImageSourceFactory, rootNode, description)
        }.Concat(
          fileResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_controller, rootNode, x)))
          .ToList();
      _fileNameSearchResultNodes.ForAll(x => rootNode.AddChild(x));
      ExpandNodes(_fileNameSearchResultNodes, expandAll);
      SwitchToFileNamesSearchResult();
    }

    public void SetDirectoryNamesSearchResult(DirectoryEntry directoryResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
      _directoryNameSearchResultNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(ImageSourceFactory, rootNode, description)
        }.Concat(
          directoryResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_controller, rootNode, x)))
          .ToList();
      _directoryNameSearchResultNodes.ForAll(x => rootNode.AddChild(x));
      ExpandNodes(_directoryNameSearchResultNodes, expandAll);
      SwitchToDirectoryNamesSearchResult();
    }

    public void SetTextSearchResult(DirectoryEntry searchResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
      _textSearchResultNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(ImageSourceFactory, rootNode, description)
        }.Concat(
          searchResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_controller, rootNode, x)))
          .ToList();
      _textSearchResultNodes.ForAll(x => rootNode.AddChild(x));
      ExpandNodes(_textSearchResultNodes, expandAll);
      SwitchToTextSearchResult();
    }

    public void FileSystemTreeComputing() {
      if (!FileSystemTreeNodes.Any()) {
        SetRootNodes(FileSystemTreeNodes, "(Loading files from Chromium enlistment...)");
      }
    }

    public void SetErrorResponse(ErrorResponse errorResponse) {
      var messages = new List<TreeViewItemViewModel>();
      if (errorResponse.IsRecoverable()) {
        // For a recoverable error, the deepest exception contains the 
        // "user friendly" error message.
        var rootError = new TextWarningItemViewModel(
          ImageSourceFactory,
          null,
          errorResponse.GetBaseError().Message);
        messages.Add(rootError);
      } else {
        // In case of non recoverable error, display a generic "user friendly"
        // message, with nested nodes for exception messages.
        var rootError = new TextErrorItemViewModel(
          ImageSourceFactory,
          null,
          "Error processing request. You may need to restart Visual Studio.");
        messages.Add(rootError);

        // Add all errors to the parent
        while (errorResponse != null) {
          rootError.Children.Add(new TextItemViewModel(ImageSourceFactory, rootError, errorResponse.Message));
          errorResponse = errorResponse.InnerError;
        }
      }

      // Update tree with error messages
      SetRootNodes(messages);
    }
  }
}
