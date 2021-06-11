using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.ComponentModelHost;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  /// <summary>
  /// Interaction logic for BuildExplorerControl.xaml
  /// </summary>
  public partial class BuildExplorerControl : UserControl {
    private IComponentModel _componentModel;

    public BuildExplorerControl() {
      InitializeComponent();

      base.DataContext = new BuildExplorerViewModel();
    }

    private BuildExplorerViewModel ViewModel { get { return (BuildExplorerViewModel)DataContext; } }

    public void OnToolWindowCreated(IServiceProvider serviceProvider) {
      var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
      _componentModel = componentModel;

      ViewModel.OnToolWindowCreated(serviceProvider);
    }

    T GetMenuEventDataContext<T>(RoutedEventArgs e) where T : class {
      return ((MenuItem)e.Source).DataContext as T;
    }

    private void SetDefaultBrowserMenuItem_Click(object sender, RoutedEventArgs e) {
      InstalledBuildViewModel build = GetMenuEventDataContext<InstalledBuildViewModel>(e);
      if (build == null)
        return;

      Debug.WriteLine("SetDefaultBrowserMenuItem_Click");
    }

    private void StartDebuggingMenuItem_Click(object sender, RoutedEventArgs e) {
      InstalledBuildViewModel build = GetMenuEventDataContext<InstalledBuildViewModel>(e);
      if (build == null)
        return;

      Debug.WriteLine("StartDebuggingMenuItem_Click");
    }

    private void StartWithoutDebuggingMenuItem_Click(object sender, RoutedEventArgs e) {
      InstalledBuildViewModel build = GetMenuEventDataContext<InstalledBuildViewModel>(e);
      if (build == null)
        return;

      Debug.WriteLine("StartWithoutDebuggingMenuItem_Click");
    }

    private void AttachDebuggerMenuItem_Click(object sender, RoutedEventArgs e) {
      InstalledBuildViewModel build = GetMenuEventDataContext<InstalledBuildViewModel>(e);
      if (build == null)
        return;

      Debug.WriteLine("AttachDebuggerMenuItem_Click");
    }

    private void DetachDebuggerMenuItem_Click(object sender, RoutedEventArgs e) {
      InstalledBuildViewModel build = GetMenuEventDataContext<InstalledBuildViewModel>(e);
      if (build == null)
        return;

      Debug.WriteLine("DetachDebuggerMenuItem_Click");
    }

    private void StopDebuggingMenuItem_Click(object sender, RoutedEventArgs e) {
      InstalledBuildViewModel build = GetMenuEventDataContext<InstalledBuildViewModel>(e);
      if (build == null)
        return;

      Debug.WriteLine("StopDebuggingMenuItem_Click");
    }
  }
}
