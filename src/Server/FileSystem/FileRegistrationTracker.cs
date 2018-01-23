// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem {
  [Export(typeof(IFileRegistrationTracker))]
  public class FileRegistrationTracker : IFileRegistrationTracker {
    private static readonly TaskId FlushFileRegistrationQueueTaskId = new TaskId("FlushFileRegistrationQueueTaskId");
    private static readonly TaskId RefreshTaskId = new TaskId("RefreshTaskId");

    private readonly IFileSystem _fileSystem;
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly ITaskQueue _taskQueue;
    private readonly FileRegistrationQueue _pendingFileRegistrations = new FileRegistrationQueue();

    /// <summary>
    /// Access to this field is serialized through tasks executed on the _taskQueue
    /// </summary>
    private readonly HashSet<FullPath> _registeredFiles = new HashSet<FullPath>();

    [ImportingConstructor]
    public FileRegistrationTracker(IFileSystem fileSystem, IProjectDiscovery projectDiscovery,
      ITaskQueueFactory taskQueueFactory) {
      _fileSystem = fileSystem;
      _projectDiscovery = projectDiscovery;
      _taskQueue = taskQueueFactory.CreateQueue("File Registration Tracker Task Queue");
    }

    public event EventHandler<ProjectsEventArgs> ProjectListChanged;
    public event EventHandler<ProjectsEventArgs> ProjectListRefreshed;

    public void RegisterFileAsync(FullPath path) {
      Logger.LogInfo("Register path \"{0}\"", path);
      _pendingFileRegistrations.Enqueue(FileRegistrationKind.Register, path);
      _taskQueue.Enqueue(FlushFileRegistrationQueueTaskId, FlushFileRegistrationQueueTask);
    }

    public void UnregisterFileAsync(FullPath path) {
      Logger.LogInfo("Unregister path \"{0}\"", path);
      _pendingFileRegistrations.Enqueue(FileRegistrationKind.Unregister, path);
      _taskQueue.Enqueue(FlushFileRegistrationQueueTaskId, FlushFileRegistrationQueueTask);
    }

    public void RefreshAsync(Action<IList<IProject>> callback) {
      Logger.LogInfo("Enqeuing file registration full refresh");
      _taskQueue.Enqueue(RefreshTaskId, token => RefreshTask(token, callback));
    }

    private void FlushFileRegistrationQueueTask(CancellationToken cancellationToken) {
      if (ProcessPendingFileRegistrations()) {
        // TODO(rpaquay): Be smarter here, don't recompute directory roots
        // that have not been affected.
        OnProjectListChanged(new ProjectsEventArgs(CollectAndSortProjectsFromRegisteredFiles()));
      }
    }

    private void RefreshTask(CancellationToken cancellationToken, Action<IList<IProject>> callback) {
      ProcessPendingFileRegistrations();
      ValidateKnownFiles();

      if (cancellationToken.IsCancellationRequested) {
        return;
      }
      var projects = CollectAndSortProjectsFromRegisteredFiles();
      callback(projects);
    }

    private IList<IProject> CollectAndSortProjectsFromRegisteredFiles() {
      return _registeredFiles
        .Select(x => _projectDiscovery.GetProject(x))
        .Where(x => x != null)
        .Distinct(new ProjectPathComparer())
        .OrderBy(x => x.RootPath)
        .ToList();
    }

    /// <summary>
    /// Sanety check: remove all files that don't exist on the file system anymore.
    /// </summary>
    private bool ValidateKnownFiles() {
      // Reset our knowledge about the file system, as a safety measure, since we don't
      // currently fully implement watching all changes in the file system that could affect
      // the cache. For example, if a ".chromium-project" file is added to a child
      // directory of a file we have been notified, it could totally change how we compute
      // the world.
      _projectDiscovery.ValidateCache();

      // We take the lock twice because we want to avoid calling "File.Exists" inside
      // the lock.
      IList<FullPath> registeredPaths = _registeredFiles.ToList();

      var deletedFileNames = registeredPaths.Where(x => !_fileSystem.FileExists(x)).ToList();

      if (deletedFileNames.Any()) {
        Logger.LogInfo("Some known files do not exist on disk anymore. Time to recompute the world.");
        deletedFileNames.ForEach(x => _registeredFiles.Remove(x));
        return true;
      }

      return false;
    }

    private bool ProcessPendingFileRegistrations() {
      var entries = _pendingFileRegistrations.DequeueAll();
      if (!entries.Any())
        return false;

      Logger.LogInfo("FlushFileRegistrationQueueTask:");
      foreach (var entry in entries) {
        Logger.LogInfo("    Path=\"{0}\", Kind={1}", entry.Path, entry.Kind);
      }

      var recompute = ValidateKnownFiles();

      // Take a snapshot of all known project paths before applying changes
      var projectPaths1 = CollectAndSortProjectsFromRegisteredFiles();

      // Apply changes
      foreach (var entry in entries) {
        switch (entry.Kind) {
          case FileRegistrationKind.Register:
            _registeredFiles.Add(entry.Path);
            break;
          case FileRegistrationKind.Unregister:
            _registeredFiles.Remove(entry.Path);
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }
      }

      // Take a snapshot after applying changes, and compare
      var newProjects = CollectAndSortProjectsFromRegisteredFiles();
      if (!projectPaths1.SequenceEqual(newProjects, new ProjectPathComparer())) {
        recompute = true;
      }

      return recompute;
    }

    protected virtual void OnProjectListChanged(ProjectsEventArgs e) {
      ProjectListChanged?.Invoke(this, e);
    }

    protected virtual void OnProjectListRefreshed(ProjectsEventArgs e) {
      ProjectListRefreshed?.Invoke(this, e);
    }
  }
}