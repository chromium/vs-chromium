// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Files.Windows;
using VsChromium.Core.Memory;
using VsChromium.Core.Threads;
using VsChromium.Protocol.Generated;
using VsChromium.Server.Cache;
using VsChromium.Server.Diagnostics;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.JsonRpc;
using VsChromium.Server.Operations;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;
using VsChromium.Server.Projects.Chromium;
using VsChromium.Server.Projects.ProjectFile;
using VsChromium.Server.Search;
using VsChromium.Server.Threads;

namespace VsChromium.Server {
  /// <summary>
  /// The composition root of the server: creates all the components of the server (all singletons)
  /// with plain constructor calls, so that the object graph is explicit, checked by the compiler, and
  /// doesn't need any reflection (no dependency injection container).
  ///
  /// Components are created on first access (in the same order as a dependency injection container
  /// would), and disposed in the reverse order of their creation by <see cref="Dispose"/>. Not thread
  /// safe: the components are created on a single thread, when the server starts (or by tests).
  /// </summary>
  public sealed class ServerComponents : IDisposable {
    private readonly List<IDisposable> _disposables = new List<IDisposable>();
    private readonly IndexCacheOptions _indexCacheOptions;

    /// <summary>
    /// The components of the server, without index cache (tests).
    /// </summary>
    public ServerComponents() : this(IndexCacheOptions.Disabled) {
    }

    public ServerComponents(IndexCacheOptions indexCacheOptions) {
      _indexCacheOptions = indexCacheOptions;
    }

    private T Create<T>(T component) {
      var disposable = component as IDisposable;
      if (disposable != null) {
        _disposables.Add(disposable);
      }
      return component;
    }

    public void Dispose() {
      for (var i = _disposables.Count - 1; i >= 0; i--) {
        _disposables[i].Dispose();
      }
      _disposables.Clear();
    }

    // Server
    private IIndexingServer _indexingServer;
    public IIndexingServer IndexingServer => _indexingServer ??= Create(new IndexingServer(DateTimeProvider, FileSystemSnapshotManager, TaskQueueFactory, SearchEngine, IndexCache));

    // Server/Diagnostics
    private ServerHistorySampler _serverHistorySampler;
    /// <summary>The history of the server state ("server/history"), started by <see cref="ServerStub"/>.</summary>
    public ServerHistorySampler ServerHistorySampler => _serverHistorySampler ??= Create(new ServerHistorySampler(SearchEngine, IndexingServer, IndexCache));

    // Server/Cache
    private IIndexCache _indexCache;
    public IIndexCache IndexCache => _indexCache ??= Create(new IndexCache(_indexCacheOptions));

    // Core/Configuration
    private IConfigurationFileLocator _configurationFileLocator;
    public IConfigurationFileLocator ConfigurationFileLocator => _configurationFileLocator ??= Create(new ConfigurationFileLocator(FileSystem));

    // Core/Threads
    private IDateTimeProvider _dateTimeProvider;
    public IDateTimeProvider DateTimeProvider => _dateTimeProvider ??= Create(new DateTimeProvider());

    // Core/Files
    private IDirectoryChangeWatcherFactory _directoryChangeWatcherFactory;
    public IDirectoryChangeWatcherFactory DirectoryChangeWatcherFactory => _directoryChangeWatcherFactory ??= Create(new DirectoryChangeWatcherFactory(FileSystem, DateTimeProvider));

    private IFileSystem _fileSystem;
    /// <summary>
    /// Windows: <see cref="WindowsFileSystem"/> (file ids of the directories, for the change
    /// journal), portable code otherwise (see "Windows specific code" in PORTING_PRINCIPLES.md).
    /// </summary>
    public IFileSystem FileSystem =>
      _fileSystem ??= Create(OperatingSystem.IsWindows() ? new WindowsFileSystem() : new PortableFileSystem());

    // Server/FileSystem
    private IFileSystemSnapshotBuilder _fileSystemSnapshotBuilder;
    public IFileSystemSnapshotBuilder FileSystemSnapshotBuilder => _fileSystemSnapshotBuilder ??= Create(new FileSystemSnapshotBuilder(FileSystem, ProgressTrackerFactory, IndexingWorkerPool, ChangeJournal));

    private IChangeJournal _changeJournal;
    private bool _changeJournalCreated;
    /// <summary>
    /// The change journal of the volumes (verification of the index cache): Windows only (see
    /// "Windows specific code" in PORTING_PRINCIPLES.md), null on the other OSes (full scans).
    /// </summary>
    public IChangeJournal ChangeJournal {
      get {
        if (!_changeJournalCreated) {
          _changeJournalCreated = true;
          _changeJournal = OperatingSystem.IsWindows() ? Create(new WindowsChangeJournal()) : null;
        }
        return _changeJournal;
      }
    }

    private IFileRegistrationTracker _fileRegistrationTracker;
    public IFileRegistrationTracker FileRegistrationTracker => _fileRegistrationTracker ??= Create(new FileRegistrationTracker(FileSystem, ProjectDiscovery, TaskQueueFactory));

    private IFileSystemSnapshotManager _fileSystemSnapshotManager;
    public IFileSystemSnapshotManager FileSystemSnapshotManager => _fileSystemSnapshotManager ??= Create(new FileSystemSnapshotManager(
      FileSystemNameFactory, FileSystem, FileRegistrationTracker, FileSystemSnapshotBuilder, OperationProcessor, ProjectDiscovery,
      DirectoryChangeWatcherFactory, TaskQueueFactory, LongRunningFileSystemTaskQueue, IndexCache));

    // Server/FileSystemContents
    private IFileContentsFactory _fileContentsFactory;
    public IFileContentsFactory FileContentsFactory => _fileContentsFactory ??= Create(new FileContentsFactory(FileSystem, ArenaAllocators));

    private ProjectArenaAllocators _arenaAllocators;
    /// <summary>The arena allocators of the file contents, one per project.</summary>
    public ProjectArenaAllocators ArenaAllocators => _arenaAllocators ??= Create(new ProjectArenaAllocators());

    // Server/FileSystemDatabase
    private IFileDatabaseSnapshotFactory _fileDatabaseSnapshotFactory;
    public IFileDatabaseSnapshotFactory FileDatabaseSnapshotFactory => _fileDatabaseSnapshotFactory ??= Create(new FileDatabaseSnapshotFactory(FileSystem, FileContentsFactory, ProgressTrackerFactory, IndexingWorkerPool, IndexCache));

    // Server/FileSystemNames
    private IFileSystemNameFactory _fileSystemNameFactory;
    public IFileSystemNameFactory FileSystemNameFactory => _fileSystemNameFactory ??= Create(new FileSystemNameFactory());

    // Server/JsonRpc
    private JsonRpcServer _jsonRpcServer;
    public JsonRpcServer JsonRpcServer => _jsonRpcServer ??= Create(new JsonRpcServer(JsonRpcOutput, JsonRpcConnection, JsonRpcEventForwarder));

    private JsonRpcConnection _jsonRpcConnection;
    public JsonRpcConnection JsonRpcConnection => _jsonRpcConnection ??= Create(new JsonRpcConnection(ProtocolRequestHandler, JsonRpcOutput));

    private JsonRpcOutput _jsonRpcOutput;
    public JsonRpcOutput JsonRpcOutput => _jsonRpcOutput ??= Create(new JsonRpcOutput());

    private IProtocolRequestHandler _protocolRequestHandler;
    public IProtocolRequestHandler ProtocolRequestHandler => _protocolRequestHandler ??= Create(new ProtocolRequestHandler(
      FileSystemSnapshotManager, SearchEngine, FileRegistrationTracker, IndexingServer, ArenaAllocators, IndexCache,
      ServerHistorySampler, ProjectDiskUsageService));

    private JsonRpcNotificationSender _jsonRpcNotificationSender;
    public JsonRpcNotificationSender JsonRpcNotificationSender => _jsonRpcNotificationSender ??= Create(new JsonRpcNotificationSender(JsonRpcOutput));

    private JsonRpcEventForwarder _jsonRpcEventForwarder;
    public JsonRpcEventForwarder JsonRpcEventForwarder => _jsonRpcEventForwarder ??= Create(new JsonRpcEventForwarder(
      JsonRpcNotificationSender, IndexingServer, FileSystemSnapshotManager, SearchEngine));

    // Server/Operations
    private IOperationIdFactory _operationIdFactory;
    public IOperationIdFactory OperationIdFactory => _operationIdFactory ??= Create(new OperationIdFactory());

    private IOperationProcessor _operationProcessor;
    public IOperationProcessor OperationProcessor => _operationProcessor ??= Create(new OperationProcessor(OperationIdFactory));

    // Server/ProgressTracking
    private IProgressTrackerFactory _progressTrackerFactory;
    public IProgressTrackerFactory ProgressTrackerFactory => _progressTrackerFactory ??= Create(new ProgressTrackerFactory(JsonRpcNotificationSender));

    // Server/Projects
    private IReadOnlyList<IProjectDiscoveryProvider> _projectDiscoveryProviders;
    public IReadOnlyList<IProjectDiscoveryProvider> ProjectDiscoveryProviders => _projectDiscoveryProviders ??= new IProjectDiscoveryProvider[] {
      Create(new ChromiumProjectDiscoveryProvider(ConfigurationFileLocator, FileSystem)),
      Create(new ProjectFileDiscoveryProvider(FileSystem)),
    };

    private IProjectDiscovery _projectDiscovery;
    public IProjectDiscovery ProjectDiscovery => _projectDiscovery ??= Create(new ProjectDiscovery(ProjectDiscoveryProviders));

    private ProjectDiskUsageService _projectDiskUsageService;
    public ProjectDiskUsageService ProjectDiskUsageService => _projectDiskUsageService ??= Create(new ProjectDiskUsageService(FileSystem, FileSystemSnapshotManager, SearchEngine));

    // Server/Search
    private ICompiledTextSearchDataFactory _compiledTextSearchDataFactory;
    public ICompiledTextSearchDataFactory CompiledTextSearchDataFactory => _compiledTextSearchDataFactory ??= Create(new CompiledTextSearchDataFactory(SearchStringParser, CompiledTextSearchProviderFactory));

    private ICompiledTextSearchProviderFactory _compiledTextSearchProviderFactory;
    public ICompiledTextSearchProviderFactory CompiledTextSearchProviderFactory => _compiledTextSearchProviderFactory ??= Create(new CompiledTextSearchProviderFactory());

    private ISearchEngine _searchEngine;
    public ISearchEngine SearchEngine => _searchEngine ??= Create(new SearchEngine(
      FileSystemSnapshotManager, FileSystemNameFactory, LongRunningFileSystemTaskQueue, FileDatabaseSnapshotFactory, ProjectDiscovery,
      CompiledTextSearchDataFactory, OperationProcessor, IndexingWorkerPool, IndexCache, ArenaAllocators));

    private ISearchStringParser _searchStringParser;
    public ISearchStringParser SearchStringParser => _searchStringParser ??= Create(new SearchStringParser());

    // Server/Threads
    private ICustomThreadPool _customThreadPool;
    public ICustomThreadPool CustomThreadPool => _customThreadPool ??= Create(new CustomThreadPool(DateTimeProvider));

    private IIndexingWorkerPool _indexingWorkerPool;
    public IIndexingWorkerPool IndexingWorkerPool => _indexingWorkerPool ??= Create(new IndexingWorkerPool());

    private ILongRunningFileSystemTaskQueue _longRunningFileSystemTaskQueue;
    public ILongRunningFileSystemTaskQueue LongRunningFileSystemTaskQueue => _longRunningFileSystemTaskQueue ??= Create(new LongRunningFileSystemTaskQueue(TaskQueueFactory));

    private ITaskQueueFactory _taskQueueFactory;
    public ITaskQueueFactory TaskQueueFactory => _taskQueueFactory ??= Create(new TaskQueueFactory(CustomThreadPool, DateTimeProvider));
  }
}
