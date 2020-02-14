// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using VsChromium.Core.Utility;
using VsChromium.Features.AutoUpdate;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class CodeSearchViewModel : ChromiumExplorerViewModelBase {
    private List<TreeViewItemViewModel> _informationMessagesNodes = new List<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _searchCodeResultNodes = new List<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _searchFilePathsResultNodes = new List<TreeViewItemViewModel>();
    private UpdateInfo _updateInfo;
    private bool _matchCase;
    private bool _matchWholeWord;
    private bool _useRegex;
    private bool _includeSymLinks;
    private bool _understandBuildOutputPaths;
    private string _indexStatusText;
    private string _indexingServerStateText;
    private string _searchCodeValue;
    private string _searchFilePathsValue;
    private string _textExtractFontFamily;
    private double _textExtractFontSize;
    private string _pathFontFamily;
    private double _pathFontSize;
    private string _displayFontFamily;
    private double _displayFontSize;
    private bool _indexingPaused;
    private string _serverStatusToolTipText;
    private bool _indexingPausedDueToError;
    private bool _indexingBusy;
    private bool _serverIsRunning;
    private bool _serverHasStarted;
    private bool _fileSystemTreeAvailable;
    private bool _expandall;

    public enum DisplayKind {
      InformationMessages,
      SearchFilePathsResult,
      SearchCodeResult,
    }

    public CodeSearchViewModel() {
      // Default values for options in toolbar.
      this.IncludeSymLinks = true;
      SetRootNodes(_informationMessagesNodes);
    }

    public void SwitchToInformationMessages() {
      SetRootNodes(_informationMessagesNodes);
    }

    public void SetInformationMessagesNoActivate(List<TreeViewItemViewModel> viewModel) {
      _informationMessagesNodes = viewModel;
    }

    public void SetInformationMessages(List<TreeViewItemViewModel> viewModel) {
      _informationMessagesNodes = viewModel;
      SetRootNodes(_informationMessagesNodes);
    }

    public void SetSearchFilePathsResult(List<TreeViewItemViewModel> viewModel) {
      _searchFilePathsResultNodes = viewModel;
      SetRootNodes(_searchFilePathsResultNodes);
    }

    public void SetSearchCodeResult(List<TreeViewItemViewModel> viewModel) {
      _searchCodeResultNodes = viewModel;
      SetRootNodes(_searchCodeResultNodes);
    }

    public DisplayKind ActiveDisplay {
      get {
        if (ReferenceEquals(ActiveRootNodes, _searchCodeResultNodes))
          return DisplayKind.SearchCodeResult;
        if (ReferenceEquals(ActiveRootNodes, _searchFilePathsResultNodes))
          return DisplayKind.SearchFilePathsResult;
        return DisplayKind.InformationMessages;
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool MatchCase {
      get { return _matchCase; }
      set {
        if (_matchCase != value) {
          _matchCase = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.MatchCase));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string MatchCaseToolTip {
      get {
        return string.Format(
          "Toggle \"Match Case\" when searching text. " +
          "\"Match case\" is currently {0}.",
          MatchCase ? "enabled" : "disabled");
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool MatchWholeWord {
      get { return _matchWholeWord; }
      set {
        if (_matchWholeWord != value) {
          _matchWholeWord = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.MatchWholeWord));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string MatchWholeWordToolTip {
      get {
        return string.Format(
          "Toggle \"Match whold word\" when searching text. " +
          "\"Match whole word\" is currently {0}.",
          MatchWholeWord ? "enabled" : "disabled");
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool UseRegex {
      get { return _useRegex; }
      set {
        if (_useRegex != value) {
          _useRegex = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.UseRegex));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string UseRegexToolTip {
      get {
        return string.Format(
          "Toggle usage of regular expressions when searching text. " +
          "Regular expressions are currently {0}.",
          UseRegex ? "enabled" : "disabled");
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool ExpandAll
    {
        get { return _expandall; }
        set
        {
            if (_expandall != value)
            {
                _expandall = value;
                OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.ExpandAll));
            }
        }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string ExpandAllTip
    {
        get
        {
            return string.Format(
                "Toggle expand all search results. " +
                "Currently {0}.",
                _expandall ? "yes" : "no");
        }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool IncludeSymLinks {
      get { return _includeSymLinks; }
      set {
        if (_includeSymLinks != value) {
          _includeSymLinks = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.IncludeSymLinks));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string IncludeSymLinksToolTip {
      get {
        return string.Format(
          "Toggle searching files inside symbolic links directories. " +
          "Symbolic links are currently {0} in search results.",
          IncludeSymLinks ? "included" : "excluded");
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool UnderstandBuildOutputPaths {
      get { return _understandBuildOutputPaths; }
      set {
        if (_understandBuildOutputPaths != value) {
          _understandBuildOutputPaths = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.UnderstandBuildOutputPaths));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string TextExtractFontFamily {
      get { return _textExtractFontFamily; }
      set {
        if (!Equals(_textExtractFontFamily, value)) {
          _textExtractFontFamily = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.TextExtractFontFamily));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public double TextExtractFontSize {
      get { return _textExtractFontSize; }
      set {
        if (Math.Abs(_textExtractFontSize - value) > 0.001) {
          _textExtractFontSize = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.TextExtractFontSize));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string PathFontFamily {
      get { return _pathFontFamily; }
      set {
        if (!Equals(_pathFontFamily, value)) {
          _pathFontFamily = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.PathFontFamily));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public double PathFontSize {
      get { return _pathFontSize; }
      set {
        if (Math.Abs(_pathFontSize - value) > 0.001) {
          _pathFontSize = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.PathFontSize));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string DisplayFontFamily {
      get { return _displayFontFamily; }
      set {
        if (!Equals(_displayFontFamily, value)) {
          _displayFontFamily = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.DisplayFontFamily));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public double DisplayFontSize {
      get { return _displayFontSize; }
      set {
        if (Math.Abs(_displayFontSize - value) > 0.001) {
          _displayFontSize = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.DisplayFontSize));
        }
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string UpdateInfoText {
      get {
        if (_updateInfo == null)
          return "";
        return string.Format("A new version ({0}) of VS Chromium is available: ", _updateInfo.Version);
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
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.UpdateInfoText));
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.UpdateInfoUrl));
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.UpdateInfoVisibility));
      }
    }

    /// <summary>
    /// Indicates if the server has entries in its file system tree, i.e. if
    /// there are known project roots.
    /// </summary>
    public bool FileSystemTreeAvailable {
      get { return _fileSystemTreeAvailable; }
      set {
        if (_fileSystemTreeAvailable != value) {
          _fileSystemTreeAvailable = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.FileSystemTreeAvailable));
        }
      }
    }

    public bool ServerHasStarted {
      get { return _serverHasStarted; }
      set {
        if (_serverHasStarted != value) {
          _serverHasStarted = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.ServerHasStarted));
        }
      }
    }

    public bool ServerIsRunning {
      get { return _serverIsRunning; }
      set {
        if (_serverIsRunning != value) {
          _serverIsRunning = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.ServerIsRunning));
        }
      }
    }

    public bool GotoPreviousEnabled {
      get { return ActiveDisplay != DisplayKind.InformationMessages; }
    }

    public bool GotoNextEnabled {
      get { return ActiveDisplay != DisplayKind.InformationMessages; }
    }

    public bool CancelSearchEnabled {
      get { return ActiveDisplay != DisplayKind.InformationMessages; }
    }

    public bool RefreshSearchResultsEnabled {
      get {
        return !string.IsNullOrEmpty(SearchFilePathsValue) ||
          !string.IsNullOrEmpty(SearchCodeValue);
      }
    }

    #region ImageSource for UI

    private ImageSource GetImageFromResource(string name) {
      if (ImageSourceFactory == null) {
        return Views.ImageSourceFactory.Instance.GetImageSource(name);
      }
      return ImageSourceFactory.GetImage(name);
    }

    public ImageSource GotoPreviousButtonImage {
      get {
        return GetImageFromResource("ArrowLeft");
      }
    }

    public ImageSource GotoNextButtonImage {
      get {
        return GetImageFromResource("ArrowRight");
      }
    }

    public ImageSource CancelSearchButtonImage {
      get {
        return GetImageFromResource("CancelSearch");
      }
    }

    public ImageSource SearchLensButtonImage {
      get {
        return GetImageFromResource("SearchLens");
      }
    }

    public ImageSource ClearSearchButtonImage {
      get {
        return GetImageFromResource("ClearSearch");
      }
    }

    public ImageSource SyncButtonImage {
      get {
        return GetImageFromResource("SyncActiveDocument");
      }
    }

    public ImageSource RefreshSearchResultsButtonImage {
      get {
        return GetImageFromResource("SearchLens");
      }
    }

    public ImageSource RefreshFileSystemTreeButtonImage {
      get {
        return GetImageFromResource("RefreshFileSystemTree");
      }
    }

    #endregion

    public string IndexStatusText {
      get { return _indexStatusText; }
      set {
        if (value == _indexStatusText)
          return;
        _indexStatusText = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.IndexStatusText));
      }
    }

    public string IndexingServerStateText {
      get { return _indexingServerStateText; }
      set {
        if (value == _indexingServerStateText)
          return;
        _indexingServerStateText = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.IndexingServerStateText));
      }
    }

    public string SearchCodeValue {
      get { return _searchCodeValue; }
      set {
        if (value == _searchCodeValue)
          return;
        _searchCodeValue = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.SearchCodeValue));
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.RefreshSearchResultsEnabled));
      }
    }

    public string SearchFilePathsValue {
      get { return _searchFilePathsValue; }
      set {
        if (value == _searchFilePathsValue)
          return;
        _searchFilePathsValue = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.SearchFilePathsValue));
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.RefreshSearchResultsEnabled));
      }
    }

    public string ServerStatusToolTipText {
      get { return _serverStatusToolTipText; }
      set {
        if (value == _serverStatusToolTipText)
          return;
        _serverStatusToolTipText = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.ServerStatusToolTipText));
      }
    }

    public bool IndexingPaused {
      get { return _indexingPaused; }
      set {
        if (value == _indexingPaused)
          return;
        _indexingPaused = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.IndexingPaused));
      }
    }

    public bool IndexingPausedDueToError {
      get { return _indexingPausedDueToError; }
      set {
        if (value == _indexingPausedDueToError)
          return;
        _indexingPausedDueToError = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.IndexingPausedDueToError));
      }
    }

    public bool IndexingBusy {
      get { return _indexingBusy; }
      set {
        if (value == _indexingBusy)
          return;
        _indexingBusy = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.IndexingBusy));
      }
    }

    protected override void OnRootNodesChanged() {
      base.OnRootNodesChanged();

      OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.GotoNextEnabled));
      OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.GotoPreviousEnabled));
      OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CancelSearchEnabled));
      OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.RefreshSearchResultsEnabled));
    }
  }
}
