// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Features.AutoUpdate;
using VsChromium.Features.BuildOutputAnalyzer;
using VsChromium.Package;
using VsChromium.ServerProxy;
using VsChromium.Settings;
using VsChromium.Threads;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  /// <summary>
  /// Interaction logic for CodeSearchControl.xaml
  /// </summary>
  public partial class CodeSearchControl : UserControl {
    // For controlling scrolling inside tree view.
    private double _treeViewHorizScrollPos;
    private bool _treeViewResetHorizScroll;
    private ScrollViewer _treeViewScrollViewer;

    private readonly IProgressBarTracker _progressBarTracker;
    private IUIRequestProcessor _uiRequestProcessor;
    private bool _swallowsRequestBringIntoView = true;
    private CodeSearchController _controller;

    public CodeSearchControl() {
      InitializeComponent();
      // Add the "VsColors" brushes to the WPF resources of the control, so that the
      // resource keys used on the XAML file can be resolved dynamically.
      this.Resources.MergedDictionaries.Add(VsResources.BuildResourceDictionary());
      base.DataContext = new CodeSearchViewModel();

      _progressBarTracker = new ProgressBarTracker(ProgressBar);

      InitComboBox(SearchCodeCombo, new ComboBoxInfo {
        TextChanged = text => { ViewModel.SearchCodeValue = text; },
        SearchFunction = RefreshSearchResults,
        NextElement = SearchFilePathsCombo,
      });
      InitComboBox(SearchFilePathsCombo, new ComboBoxInfo {
        TextChanged = text => { ViewModel.SearchFilePathsValue = text; },
        SearchFunction = RefreshSearchResults,
        PreviousElement = SearchCodeCombo,
        NextElement = FileTreeView,
        InitialItems = {
          "*",
          "*.c;*.cpp;*.cxx;*.cc;*.tli;*.tlh;*.h;*.hh;*.hpp;*.hxx;*.hh;*.inl;*.rc;*.resx;*.idl;*.asm;*.inc",
          "*.htm;*.html;*.xml;*.gif;*.jpg;*.png;*.css;*.disco;*.js;*.srf",
          "*.xml;*.xsl;*.xslt;*.xsd;*.dtd",
          "*.txt",
          "*.cs;*.resx;*.resw;*.xsd;*.wsdl;*.xaml;*.xml;*.htm;*.html;*.css",
          "*.vb;*.resx;*.resw;*.xsd;*.wsdl;*.xaml;*.xml;*.htm;*.html;*.css",
          "*.*",
        }
      });
    }

    /// <summary>
    /// Called when Visual Studio creates our container ToolWindow.
    /// </summary>
    public void OnVsToolWindowCreated(IServiceProvider serviceProvider) {
      var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));

      _uiRequestProcessor = componentModel.DefaultExportProvider.GetExportedValue<IUIRequestProcessor>();

      var standarImageSourceFactory = componentModel.DefaultExportProvider.GetExportedValue<IStandarImageSourceFactory>();
      _controller = new CodeSearchController(
        this,
        _uiRequestProcessor,
        componentModel.DefaultExportProvider.GetExportedValue<IUIDelayedOperationProcessor>(),
        componentModel.DefaultExportProvider.GetExportedValue<IFileSystemTreeSource>(),
        componentModel.DefaultExportProvider.GetExportedValue<ITypedRequestProcessProxy>(),
        _progressBarTracker,
        standarImageSourceFactory,
        componentModel.DefaultExportProvider.GetExportedValue<IWindowsExplorer>(),
        componentModel.DefaultExportProvider.GetExportedValue<IClipboard>(),
        componentModel.DefaultExportProvider.GetExportedValue<ISynchronizationContextProvider>(),
        componentModel.DefaultExportProvider.GetExportedValue<IOpenDocumentHelper>(),
        componentModel.DefaultExportProvider.GetExportedValue<ITextDocumentTable>(),
        componentModel.DefaultExportProvider.GetExportedValue<IEventBus>(),
        componentModel.DefaultExportProvider.GetExportedValue<IGlobalSettingsProvider>(),
        componentModel.DefaultExportProvider.GetExportedValue<IBuildOutputParser>(),
        componentModel.DefaultExportProvider.GetExportedValue<IVsEditorAdaptersFactoryService>());
      _controller.Start();
      // TODO(rpaquay): leaky abstraction. We need this because the ViewModel
      // exposes pictures from Visual Studio resources.
      ViewModel.ImageSourceFactory = standarImageSourceFactory;

      // Hookup property changed notifier
      ViewModel.PropertyChanged += ViewModel_PropertyChanged;
      ViewModel.RootNodesChanged += ViewModelOnRootNodesChanged;
    }

    private void ViewModelOnRootNodesChanged(object sender, EventArgs eventArgs) {
      //FileTreeView.Items.Refresh();
      //FileTreeView.UpdateLayout();
    }

    public CodeSearchViewModel ViewModel {
      get {
        return (CodeSearchViewModel)DataContext;
      }
    }

    public UpdateInfo UpdateInfo {
      get { return ViewModel.UpdateInfo; }
      set { ViewModel.UpdateInfo = value; }
    }

    public ICodeSearchController Controller {
      get { return _controller; }
    }

    private void InitComboBox(EditableComboBox comboBox, ComboBoxInfo info) {
      comboBox.DataContext = new StringListViewModel(info.InitialItems);
      comboBox.TextChanged += (s, e) => {
        info.TextChanged(comboBox.Text);
        info.SearchFunction(false);
      };
      comboBox.KeyDown += (s, e) => {
        if ((e.KeyboardDevice.Modifiers == ModifierKeys.None) &&
            (e.Key == Key.Return || e.Key == Key.Enter)) {
          info.SearchFunction(true);
        }
      };

      if (info.PreviousElement != null) {
        comboBox.PrePreviewKeyDown += (s, e) => {
          if (e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key == Key.Up) {
            if (!comboBox.IsDropDownOpen) {
              info.PreviousElement.Focus();
              e.Handled = true;
            }
          }
        };
      }

      if (info.NextElement != null) {
        comboBox.PrePreviewKeyDown += (s, e) => {
          if (e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key == Key.Down) {
            if (!comboBox.IsDropDownOpen) {
              info.NextElement.Focus();
              e.Handled = true;
            }
          }
        };
      }
    }

    private class ComboBoxInfo {
      public ComboBoxInfo() {
        InitialItems = new List<string>();
      }

      public Action<string> TextChanged { get; set; }
      public Action<bool> SearchFunction { get; set; }
      public UIElement PreviousElement { get; set; }
      public UIElement NextElement { get; set; }
      public List<string> InitialItems { get; }
    }

    public void SwallowsRequestBringIntoView(bool value) {
      _swallowsRequestBringIntoView = value;
    }

    #region WPF Event handlers

    void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e) {
      // Handle property change for the ViewModel.
      if (e.PropertyName == ReflectionUtils.GetPropertyName(ViewModel, x => x.MatchCase) ||
          e.PropertyName == ReflectionUtils.GetPropertyName(ViewModel, x => x.MatchWholeWord) ||
          e.PropertyName == ReflectionUtils.GetPropertyName(ViewModel, x => x.UseRegex) ||
          e.PropertyName == ReflectionUtils.GetPropertyName(ViewModel, x => x.IncludeSymLinks) ||
          e.PropertyName == ReflectionUtils.GetPropertyName(ViewModel, x => x.UnderstandBuildOutputPaths))  {
        RefreshSearchResults(true);
      }
    }

    private void RefreshSearchResults(bool immediate) {
      Controller.PerformSearch(immediate);
    }

    private void TreeViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) {
      var tvi = sender as TreeViewItem;
      if (tvi == null)
        return;

      if (!tvi.IsSelected)
        return;

      if (Controller.ExecuteOpenCommandForItem(tvi.DataContext as TreeViewItemViewModel))
        e.Handled = true;
    }

    private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
      if (_swallowsRequestBringIntoView) {
        // This prevents the tree view for scrolling horizontally to make the
        // selected item as visibile as possible. This is useful for
        // "SearchCode", as text extracts are usually wide enough to make tree
        // view navigation annoying when they are selected.
        e.Handled = true;
        return;
      }

      // Find the scroll viewer and hook up scroll changed event handler.
      if (this._treeViewScrollViewer == null) {
        this._treeViewScrollViewer = this.FileTreeView.Template.FindName("_tv_scrollviewer_", this.FileTreeView) as ScrollViewer;
        if (_treeViewScrollViewer != null) {
          this._treeViewScrollViewer.ScrollChanged += this.TreeViewScrollViewer_ScrollChanged;
        }
      }

      // If we got a scroll viewer, remember the horizontal offset so we can
      // restore it in the scroll changed event.
      if (_treeViewScrollViewer != null) {
        this._treeViewResetHorizScroll = true;
        this._treeViewHorizScrollPos = this._treeViewScrollViewer.HorizontalOffset;
      }
      e.Handled = false;
    }

    private void TreeViewScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
      Debug.Assert(this._treeViewScrollViewer != null);

      if (this._treeViewResetHorizScroll) {
        this._treeViewScrollViewer.ScrollToHorizontalOffset(this._treeViewHorizScrollPos);
        this._treeViewResetHorizScroll = false;
      }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
      // Open the default web browser to the update URL.
      Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
      e.Handled = true;
    }

    private void FileTreeView_OnPreviewKeyDown(object sender, KeyEventArgs e) {
      if (e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key == Key.Return) {
        e.Handled = Controller.ExecuteOpenCommandForItem(
          FileTreeView.SelectedItem as TreeViewItemViewModel);
      }

      if (e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key == Key.Up) {
        // If topmost item is selected, move selection to bottom combo box
        var item = FileTreeView.SelectedItem as TreeViewItemViewModel;
        if (item != null) {
          var parent = item.ParentViewModel as RootTreeViewItemViewModel;
          if (parent != null) {
            if (item == parent.Children.FirstOrDefault()) {
              SearchFilePathsCombo.Focus();
              e.Handled = true;
            }
          }
        }
      }
    }

    /// <summary>
    /// Ensures the item right-clicked on is selected before showing the context
    /// menu.
    /// </summary>
    private void FileTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
      var source = e.OriginalSource as DependencyObject;
      if (source == null)
        return;

      var treeViewItem = WpfUtilities.VisualTreeGetParentOfType<TreeViewItem>(source);
      if (treeViewItem == null)
        return;

      treeViewItem.Focus();
      e.Handled = true;
    }

    private void RefreshSearchResultsButton_Click(object sender, RoutedEventArgs e) {
      Logger.WrapActionInvocation(
        () => RefreshSearchResults(true));
    }

    private void RefreshIndexButton_Click(object sender, RoutedEventArgs e) {
      Logger.WrapActionInvocation(
        () => Controller.RefreshFileSystemTree());
    }

    private void GotoPrevious_Click(object sender, RoutedEventArgs e) {
      Controller.NavigateToPreviousLocation();
    }

    private void GotoNext_Click(object sender, RoutedEventArgs e) {
      Controller.NavigateToNextLocation();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) {
      Controller.CancelSearch();
    }

    private void ClearFilePathsPattern_Click(object sender, RoutedEventArgs e) {
      SearchFilePathsCombo.Text = "";
      RefreshSearchResults(true);
    }

    private void ClearSearchCode_Click(object sender, RoutedEventArgs e) {
      SearchCodeCombo.Text = "";
      RefreshSearchResults(true);
    }

    #endregion
  }
}
