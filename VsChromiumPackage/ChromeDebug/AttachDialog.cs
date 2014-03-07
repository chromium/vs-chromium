// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VsChromiumCore.Win32.Processes;
using VsChromiumCore.Processes;
using VsChromiumCore.Utility;
using VsChromiumCore.Win32.Shell;
using System.Runtime.InteropServices;

namespace VsChromiumPackage.ChromeDebug {
  // The form that is displayed to allow the user to select processes to attach to.  Note that we
  // cannot interact with the DTE object from here (I assume this is because the dialog is running
  // on a different thread, although I don't fully understand), so any access to the DTE object
  // will have to be done through events that get posted back to the main package thread.
  public partial class AttachDialog : Form {
    private class ProcessViewItem : ListViewItem {
      public ProcessViewItem() {
        Category = ProcessCategory.Other;
        MachineType = MachineType.Unknown;
      }

      public string Exe;
      public int ProcessId;
      public int SessionId;
      public string Title;
      public string DisplayCmdLine;
      public string[] CmdLineArgs;
      public ProcessCategory Category;
      public MachineType MachineType;

      public NtProcess Process;
    }

    private readonly Dictionary<ProcessCategory, List<ProcessViewItem>> _loadedProcessTable = null;
    private readonly Dictionary<ProcessCategory, ListViewGroup> _processGroups = null;

    public AttachDialog() {
      InitializeComponent();

      _loadedProcessTable = new Dictionary<ProcessCategory, List<ProcessViewItem>>();
      _processGroups = new Dictionary<ProcessCategory, ListViewGroup>();

      // Create and initialize the groups and process lists only once. On a reset
      // we don't clear the groups manually, clearing the list view should clear the
      // groups. And we don't clear the entire processes_ dictionary, only the
      // individual buckets inside the dictionary.
      foreach (object value in Enum.GetValues(typeof(ProcessCategory))) {
        var category = (ProcessCategory)value;

        var group = new ListViewGroup(category.ToGroupTitle());
        _processGroups[category] = group;
        listViewProcesses.Groups.Add(group);

        _loadedProcessTable[category] = new List<ProcessViewItem>();
      }
    }

    // Provides an iterator that evaluates to the process ids of the entries that are selected
    // in the list view.
    public IEnumerable<int> SelectedItems {
      get {
        foreach (ProcessViewItem item in listViewProcesses.SelectedItems)
          yield return item.ProcessId;
      }
    }

    private void AttachDialog_Load(object sender, EventArgs e) {
      RepopulateListView();
    }

    // Remove command line arguments that we aren't interested in displaying as part of the command
    // line of the process.
    private string[] FilterCommandLine(string[] args) {
      Func<string, int, bool> allowArgument = delegate(string arg, int index) {
        if (index == 0)
          return false;
        return !arg.StartsWith("--force-fieldtrials", StringComparison.CurrentCultureIgnoreCase);
      };

      // The force-fieldtrials command line option makes the command line view useless, so remove
      // it.  Also remove args[0] since that is the process name.
      args = args.Where(allowArgument).ToArray();
      return args;
    }

    private void ReloadNativeProcessInfo() {
      foreach (var list in _loadedProcessTable.Values) {
        list.Clear();
      }

      Process[] chromes = Process.GetProcessesByName("chrome");
      Process[] delegate_executes = Process.GetProcessesByName("delegate_execute");
      Process[] processes = new Process[chromes.Length + delegate_executes.Length];
      chromes.CopyTo(processes, 0);
      delegate_executes.CopyTo(processes, chromes.Length);

      foreach (Process p in processes) {
        var item = new ProcessViewItem();
        try {
          item.Process = new NtProcess(p.Id);
          if (item.Process.CommandLine != null) {
            item.CmdLineArgs = ChromeUtility.SplitArgs(item.Process.CommandLine);
            item.DisplayCmdLine = GetFilteredCommandLineString(item.CmdLineArgs);
          }
          item.MachineType = item.Process.MachineType;
        }
        catch {
          // Generally speaking, an exception here means the process is privileged and we cannot
          // get any information about the process.  For those processes, we will just display the
          // information that the Framework gave us in the Process structure.
        }

        item.ProcessId = p.Id;
        item.SessionId = p.SessionId;
        item.Title = p.MainWindowTitle;
        item.Exe = p.ProcessName;
        if (item.CmdLineArgs != null) {
            item.Category = DetermineProcessCategory(item.Process.Win32ProcessImagePath,
                                                     item.CmdLineArgs);
        }

        var icon = LoadIconForProcess(item.Process);
        var items = _loadedProcessTable[item.Category];
        item.Group = _processGroups[item.Category];
        items.Add(item);
      }
    }

    private Icon LoadIconForProcess(NtProcess process) {
      SHFileInfo info = new SHFileInfo(true);
      SHGFI flags = SHGFI.Icon | SHGFI.SmallIcon | SHGFI.OpenIcon | SHGFI.UseFileAttributes;
      int cbFileInfo = Marshal.SizeOf(info);
      VsChromiumCore.Win32.Shell.NativeMethods.SHGetFileInfo(
        process.Win32ProcessImagePath, 256, ref info, (uint)cbFileInfo, (uint)flags);
      return Icon.FromHandle(info.hIcon);
    }

    // Filter the command line arguments to remove extraneous arguments that we don't wish to
    // display.
    private string GetFilteredCommandLineString(string[] args) {
      if (args == null || args.Length == 0)
        return string.Empty;

      args = FilterCommandLine(args);
      return string.Join(" ", args, 0, args.Length);
    }

    // Using a heuristic based on the command line, tries to determine what type of process this
    // is.
    private ProcessCategory DetermineProcessCategory(string imagePath, string[] cmdline) {
      if (cmdline == null || cmdline.Length == 0)
        return ProcessCategory.Other;

      string file = Path.GetFileName(imagePath);
      if (file.Equals("delegate_execute.exe", StringComparison.CurrentCultureIgnoreCase))
        return ProcessCategory.DelegateExecute;
      else if (file.Equals("chrome.exe", StringComparison.CurrentCultureIgnoreCase)) {
        if (cmdline.Contains("--type=renderer"))
          return ProcessCategory.Renderer;
        else if (cmdline.Contains("--type=plugin") || cmdline.Contains("--type=ppapi"))
          return ProcessCategory.Plugin;
        else if (cmdline.Contains("--type=gpu-process"))
          return ProcessCategory.Gpu;
        else if (cmdline.Contains("--type=service"))
          return ProcessCategory.Service;
        else if (cmdline.Any(arg => arg.StartsWith("-ServerName")))
          return ProcessCategory.MetroViewer;
        else
          return ProcessCategory.Browser;
      } else
        return ProcessCategory.Other;
    }

    private void InsertCategoryItems(ProcessCategory category) {
      foreach (ProcessViewItem item in _loadedProcessTable[category]) {
        item.Text = item.Exe;
        item.SubItems.Add(item.ProcessId.ToString());
        item.SubItems.Add(item.Title);
        item.SubItems.Add(item.MachineType.ToString());
        item.SubItems.Add(item.SessionId.ToString());
        item.SubItems.Add(item.DisplayCmdLine);
        listViewProcesses.Items.Add(item);

        Icon icon = LoadIconForProcess(item.Process);
        if (icon != null) {
          item.ImageList.Images.Add(icon);
          item.ImageIndex = item.ImageList.Images.Count - 1;
        }
      }
    }

    private void AutoResizeColumns() {
      // First adjust to the width of the headers, since it's fast.
      listViewProcesses.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

      // Save the widths so we can use them again later.
      List<int> widths = new List<int>();
      foreach (ColumnHeader header in listViewProcesses.Columns)
        widths.Add(header.Width);

      // Now let Windows do the slow adjustment based on the content.
      listViewProcesses.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

      // Finally, iterate over each column, and resize those columns that just got smaller.
      int total = 0;
      for (int i = 0; i < listViewProcesses.Columns.Count; ++i) {
        // Resize to the largest of the two, but don't let it go over a pre-defined maximum.
        int max = Math.Max(listViewProcesses.Columns[i].Width, widths[i]);
        int capped = Math.Min(max, 300);

        // We do still want to fill up the available space in the list view however, so if we're
        // under then we can fill.
        int globalMinWidth = listViewProcesses.Width - SystemInformation.VerticalScrollBarWidth;
        if (i == listViewProcesses.Columns.Count - 1 && (total + capped) < (globalMinWidth - 4))
          capped = globalMinWidth - total - 4;

        total += capped;
        listViewProcesses.Columns[i].Width = capped;
      }
    }

    private void RepopulateListView() {
      listViewProcesses.Items.Clear();
      listViewProcesses.SmallImageList = new ImageList();
      listViewProcesses.SmallImageList.ImageSize = new Size(16, 16);

      ReloadNativeProcessInfo();

      InsertCategoryItems(ProcessCategory.Browser);
      InsertCategoryItems(ProcessCategory.Renderer);
      InsertCategoryItems(ProcessCategory.Gpu);
      InsertCategoryItems(ProcessCategory.Plugin);
      InsertCategoryItems(ProcessCategory.MetroViewer);
      InsertCategoryItems(ProcessCategory.Service);
      InsertCategoryItems(ProcessCategory.DelegateExecute);

      AutoResizeColumns();
    }

    private void buttonRefresh_Click(object sender, EventArgs e) {
      RepopulateListView();
    }

    private void buttonAttach_Click(object sender, EventArgs e) {
      System.Diagnostics.Debug.WriteLine("Closing dialog.");
      Close();
    }
  }
}
