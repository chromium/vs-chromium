// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Threads;
using VsChromium.Package;
using VsChromium.Threads;

namespace VsChromium.Features.IndexServerInfo {
  [Export(typeof(IShowServerInfoService))]
  public class ShowServerInfoService : IShowServerInfoService {
    private readonly IDispatchThreadServerRequestExecutor _dispatchThreadServerRequestExecutor;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IShellHost _shellHost;

    [ImportingConstructor]
    public ShowServerInfoService(IDispatchThreadServerRequestExecutor dispatchThreadServerRequestExecutor,
      IDateTimeProvider dateTimeProvider, IShellHost shellHost) {
      _dispatchThreadServerRequestExecutor = dispatchThreadServerRequestExecutor;
      _dateTimeProvider = dateTimeProvider;
      _shellHost = shellHost;
    }

    public void ShowServerStatusDialog(bool forceGarbageCollection) {
      var dialog = new ServerStatusDialog();
      dialog.HasMinimizeButton = false;
      dialog.HasMaximizeButton = false;
      dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
      dialog.ViewModel.Waiting = true;

      var isClosed = false;
      dialog.Closed += (sender, args) => isClosed = true;

      FetchDatabaseStatisticsImpl(Guid.NewGuid().ToString(), TimeSpan.Zero, forceGarbageCollection, response => {
        if (isClosed) {
          return;
        }

        dialog.ViewModel.Waiting = false;
        dialog.ViewModel.ProjectCount = response.ProjectCount;
        dialog.ViewModel.ShowServerDetailsInvoked += (sender, args) => {
          // Close dialog to avoid too many nested modal dialogs, which can be
          // confusing.
          dialog.Close();
          OnShowServerDetailsInvoked();
        };
        var message = new StringBuilder();
        message.AppendFormat("-- {0} --\r\n", GetIndexingServerStatusText(response));
        message.AppendLine();
        message.AppendFormat("{0}\r\n", GetIndexingServerStatusToolTipText(response));
        dialog.ViewModel.ServerStatus = message.ToString().TrimSuffix("\r\n");
        message.Clear();

        message.AppendFormat("Directory/project count: {0:n0}\r\n", response.ProjectCount);
        message.AppendFormat("Total file count: {0:n0}\r\n", response.FileCount);
        message.AppendFormat("Searchable file count: {0:n0}\r\n", response.SearchableFileCount);
        if (response.IndexLastUpdatedUtc != DateTime.MinValue && response.SearchableFileCount > 0) {
          message.AppendFormat("Last updated: {0} ({1} {2})\r\n",
            HumanReadableDuration(response.IndexLastUpdatedUtc),
            response.IndexLastUpdatedUtc.ToLocalTime().ToShortDateString(),
            response.IndexLastUpdatedUtc.ToLocalTime().ToLongTimeString());
        } else {
          message.AppendFormat("Last updated: {0}\r\n", "n/a (index is empty)");
        }

        dialog.ViewModel.IndexStatus = message.ToString().TrimSuffix("\r\n");
        message.Clear();

        message.AppendFormat("Managed memory: {0:n2} MB\r\n", (double) response.ServerGcMemoryUsage / (1024 * 1024));
        message.AppendFormat("Native memory: {0:n2} MB\r\n", (double) response.ServerNativeMemoryUsage / (1024 * 1024));
        dialog.ViewModel.MemoryStatus = message.ToString().TrimSuffix("\r\n");
      });

      dialog.ShowModal();
    }

    private void OnShowServerDetailsInvoked() {
      // Prepare the dialog window
      var dialog = new ServerDetailsDialog();
      dialog.HasMinimizeButton = false;
      dialog.HasMaximizeButton = false;
      dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

      // Post async-request to fetch the server details
      _dispatchThreadServerRequestExecutor.Post(
        new DispatchThreadServerRequest {
          Id = Guid.NewGuid().ToString(),
          Delay = TimeSpan.Zero,
          Request = new GetDatabaseDetailsRequest {
            MaxFilesByExtensionDetailsCount = 500,
            MaxLargeFilesDetailsCount = 4000,
          },
          OnDispatchThreadError = error => {
            dialog.ViewModel.Waiting = false;
            _shellHost.ShowErrorMessageBox("Error retrieving server index details", error);
          },
          OnDispatchThreadSuccess = typedResponse => {
            var response = (GetDatabaseDetailsResponse) typedResponse;
            var projectDetails = response.Projects.Select(x => new ProjectDetailsViewModel {
              ProjectDetails = x
            }).ToList();
            foreach (var x in projectDetails) {
              x.ShowProjectConfigurationInvoked += (sender, args) => { ShowProjectConfiguration(x); };
            }

            dialog.ViewModel.Projects.AddRange(projectDetails);
            if (dialog.ViewModel.Projects.Count > 0) {
              dialog.ViewModel.SelectedProject = dialog.ViewModel.Projects[0];
            }

            dialog.ViewModel.Waiting = false;
          }
        });

      // Show the dialog right away, waiting for a response for the above request
      // (The dialog shows a "Please wait..." message while waiting)
      dialog.ShowModal();
    }

    public void ShowProjectIndexDetailsDialog(string path) {
      // Prepare the dialog window
      var dialog = new ProjectDetailsDialog();
      dialog.HasMinimizeButton = false;
      dialog.HasMaximizeButton = false;
      dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
      dialog.ViewModel.ShowProjectConfigurationInvoked += (sender, args) => {
        ShowProjectConfiguration(dialog.ViewModel);
      };

      // Post async-request to fetch the project details
      _dispatchThreadServerRequestExecutor.Post(new DispatchThreadServerRequest {
        Id = Guid.NewGuid().ToString(),
        Delay = TimeSpan.FromSeconds(0.0),
        Request = new GetProjectDetailsRequest {
          ProjectPath = path,
          MaxFilesByExtensionDetailsCount = 500,
          MaxLargeFilesDetailsCount = 4000
        },
        OnDispatchThreadError = error => {
          dialog.ViewModel.Waiting = false;
          _shellHost.ShowErrorMessageBox("Error retrieving project index details", error);
        },
        OnDispatchThreadSuccess = typedResponse => {
          var response = (GetProjectDetailsResponse) typedResponse;
          dialog.ViewModel.ProjectDetails = response.ProjectDetails;
          dialog.ViewModel.Waiting = false;
        },
      });

      // Show the dialog right away, waiting for a response for the above request
      // (The dialog shows a "Please wait..." message while waiting)
      dialog.ShowModal();
    }

    public void ShowDirectoryIndexDetailsDialog(string path) {
      // Prepare the dialog window
      var dialog = new DirectoryDetailsDialog();
      dialog.HasMinimizeButton = false;
      dialog.HasMaximizeButton = false;
      dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

      // Post async-request to fetch the project details
      _dispatchThreadServerRequestExecutor.Post(new DispatchThreadServerRequest {
        Id = Guid.NewGuid().ToString(),
        Delay = TimeSpan.FromSeconds(0.0),
        Request = new GetDirectoryDetailsRequest {
          Path = path,
          MaxFilesByExtensionDetailsCount = 500,
          MaxLargeFilesDetailsCount = 4000
        },
        OnDispatchThreadError = error => {
          dialog.ViewModel.Waiting = false;
          _shellHost.ShowErrorMessageBox("Error retrieving server index details for directory", error);
        },
        OnDispatchThreadSuccess = typedResponse => {
          var response1 = (GetDirectoryDetailsResponse) typedResponse;
          dialog.ViewModel.DirectoryDetails = response1.DirectoryDetails;
          dialog.ViewModel.Waiting = false;
        },
      });

      // Show the dialog right away, waiting for a response for the above request
      // (The dialog shows a "Please wait..." message while waiting)
      dialog.ShowModal();
    }

    private void ShowProjectConfiguration(ProjectDetailsViewModel projectDetailsViewModel) {
      var dialog = new ProjectConfigurationDetailsDialog();
      dialog.HasMinimizeButton = false;
      dialog.HasMaximizeButton = false;
      dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
      dialog.ViewModel = projectDetailsViewModel.ProjectDetails;
      dialog.ShowModal();
    }

    public void FetchDatabaseStatistics(Action<GetDatabaseStatisticsResponse> callback) {
      FetchDatabaseStatisticsImpl(nameof(FetchDatabaseStatistics), null, false, callback);
    }

    public string GetIndexStatusText(GetDatabaseStatisticsResponse response) {
      var memoryUsageMb = (double) response.ServerNativeMemoryUsage / 1024L / 1024L;
      var message = String.Format("Index: {0:n0} files - {1:n0} MB", response.SearchableFileCount, memoryUsageMb);
      return message;
    }

    public string GetIndexingServerStatusText(GetDatabaseStatisticsResponse response) {
      switch (response.ServerStatus) {
        case IndexingServerStatus.Idle:
          return "Idle";
        case IndexingServerStatus.Paused:
          return "Paused";
        case IndexingServerStatus.Yield:
          return "Yield";
        case IndexingServerStatus.Busy:
          return "Busy";
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    public string GetIndexingServerStatusToolTipText(GetDatabaseStatisticsResponse response) {
      switch (response.ServerStatus) {
        case IndexingServerStatus.Idle:
          return "The server is idle and the index is up to date.\r\n" +
                 "The index is automatically updated as files change on disk.";
        case IndexingServerStatus.Paused:
          return "The server is paused and the index may be out of date.\r\n" +
                 "The index is not automatically updated as files change on disk.\r\n" +
                 "Press the \"Run\" button to resume automatic indexing now.";
        case IndexingServerStatus.Yield:
          return "The server is paused due to heavy disk activity and the index may be out of date.\r\n" +
                 "The index is not automatically updated as files change on disk.\r\n" +
                 "The server will attempt to update the index in a few minutes.\r\n" +
                 "Press the \"Run\" button to resume automatic indexing now.";
        case IndexingServerStatus.Busy:
          return "The server is working on updating the index to match the contents of files on disk.\r\n" +
                 "Press the \"Pause\" button to pause indexing.";
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    public string HumanReadableDuration(DateTime utcTime) {
      var span = _dateTimeProvider.UtcNow - utcTime;
      if (span.TotalSeconds <= 5) {
        return "a few seconds ago";
      }

      if (span.TotalSeconds <= 50) {
        return "less than 1 minute ago";
      }

      if (span.TotalMinutes <= 1) {
        return "about 1 minute ago";
      }

      if (span.TotalMinutes <= 60) {
        return string.Format("about {0:n0} minutes ago", Math.Ceiling(span.TotalMinutes));
      }

      if (span.TotalHours <= 1.5) {
        return "about one hour ago";
      }

      if (span.TotalHours <= 24) {
        return string.Format("about {0:n0} hours ago", Math.Ceiling(span.TotalHours));
      }

      return "more than one day ago";
    }

    public void FetchDatabaseStatisticsImpl(string requestId, TimeSpan? delay, bool forceGarbageCollect,
      Action<GetDatabaseStatisticsResponse> callback) {
      var request = new DispatchThreadServerRequest {
        Id = requestId,
        Request = new GetDatabaseStatisticsRequest {
          ForceGabageCollection = forceGarbageCollect
        },
        OnDispatchThreadSuccess = typedResponse => {
          var response = (GetDatabaseStatisticsResponse) typedResponse;
          callback(response);
        }
      };
      if (delay != null) {
        request.Delay = delay.Value;
      }

      _dispatchThreadServerRequestExecutor.Post(request);
    }
  }
}
