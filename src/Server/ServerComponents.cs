// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
using VsChromium.Core.Threads;
using VsChromium.Protocol.Generated;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Ipc;
using VsChromium.Server.Ipc.ProtocolHandlers;
using VsChromium.Server.Ipc.TypedEvents;
using VsChromium.Server.Ipc.TypedMessageHandlers;
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
  ///
  /// A server process speaks a single protocol (<see cref="ServerProtocolMode"/>): the components of
  /// the other protocol are not available (their properties throw <see
  /// cref="InvalidOperationException"/>).
  /// </summary>
  public sealed class ServerComponents : IDisposable {
    private readonly List<IDisposable> _disposables = new List<IDisposable>();
    private readonly ServerProtocolMode _mode;

    public ServerComponents(ServerProtocolMode mode) {
      _mode = mode;
    }

    public ServerProtocolMode Mode => _mode;

    private T Create<T>(T component) {
      var disposable = component as IDisposable;
      if (disposable != null) {
        _disposables.Add(disposable);
      }
      return component;
    }

    /// <summary>Throws if the component <paramref name="name"/> of <paramref name="mode"/> is not available.</summary>
    private void RequireMode(ServerProtocolMode mode, string name) {
      if (_mode != mode) {
        throw new InvalidOperationException($"Component \"{name}\" is only available in the {mode} protocol mode (current mode: {_mode})");
      }
    }

    private T CreateFor<T>(ServerProtocolMode mode, string name, Func<T> factory) {
      RequireMode(mode, name);
      return Create(factory());
    }

    public void Dispose() {
      for (var i = _disposables.Count - 1; i >= 0; i--) {
        _disposables[i].Dispose();
      }
      _disposables.Clear();
    }

    // Server
    private IServer _server;
    public IServer Server => _server ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(Server),
      () => new IpcServer(ReceiveRequestsThread, SendResponsesThread, TypedEventForwarder));

    private IIndexingServer _indexingServer;
    public IIndexingServer IndexingServer => _indexingServer ??= Create(new IndexingServer(DateTimeProvider, FileSystemSnapshotManager, TaskQueueFactory, SearchEngine));

    // Core/Configuration
    private IConfigurationFileLocator _configurationFileLocator;
    public IConfigurationFileLocator ConfigurationFileLocator => _configurationFileLocator ??= Create(new ConfigurationFileLocator(FileSystem));

    // Core/Ipc
    private IIpcRequestIdFactory _ipcRequestIdFactory;
    public IIpcRequestIdFactory IpcRequestIdFactory => _ipcRequestIdFactory ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(IpcRequestIdFactory),
      () => new IpcRequestIdFactory());

    // Core/Threads
    private IDateTimeProvider _dateTimeProvider;
    public IDateTimeProvider DateTimeProvider => _dateTimeProvider ??= Create(new DateTimeProvider());

    // Core/Files
    private IDirectoryChangeWatcherFactory _directoryChangeWatcherFactory;
    public IDirectoryChangeWatcherFactory DirectoryChangeWatcherFactory => _directoryChangeWatcherFactory ??= Create(new DirectoryChangeWatcherFactory(FileSystem, DateTimeProvider));

    private IFileSystem _fileSystem;
    public IFileSystem FileSystem => _fileSystem ??= Create(new PortableFileSystem());

    // Server/FileSystem
    private IFileSystemSnapshotBuilder _fileSystemSnapshotBuilder;
    public IFileSystemSnapshotBuilder FileSystemSnapshotBuilder => _fileSystemSnapshotBuilder ??= Create(new FileSystemSnapshotBuilder(FileSystem, ProgressTrackerFactory));

    private IFileRegistrationTracker _fileRegistrationTracker;
    public IFileRegistrationTracker FileRegistrationTracker => _fileRegistrationTracker ??= Create(new FileRegistrationTracker(FileSystem, ProjectDiscovery, TaskQueueFactory));

    private IFileSystemSnapshotManager _fileSystemSnapshotManager;
    public IFileSystemSnapshotManager FileSystemSnapshotManager => _fileSystemSnapshotManager ??= Create(new FileSystemSnapshotManager(
      FileSystemNameFactory, FileSystem, FileRegistrationTracker, FileSystemSnapshotBuilder, OperationProcessor, ProjectDiscovery,
      DirectoryChangeWatcherFactory, TaskQueueFactory, LongRunningFileSystemTaskQueue));

    // Server/FileSystemContents
    private IFileContentsFactory _fileContentsFactory;
    public IFileContentsFactory FileContentsFactory => _fileContentsFactory ??= Create(new FileContentsFactory(FileSystem));

    // Server/FileSystemDatabase
    private IFileDatabaseSnapshotFactory _fileDatabaseSnapshotFactory;
    public IFileDatabaseSnapshotFactory FileDatabaseSnapshotFactory => _fileDatabaseSnapshotFactory ??= Create(new FileDatabaseSnapshotFactory(FileSystem, FileContentsFactory, ProgressTrackerFactory));

    // Server/FileSystemNames
    private IFileSystemNameFactory _fileSystemNameFactory;
    public IFileSystemNameFactory FileSystemNameFactory => _fileSystemNameFactory ??= Create(new FileSystemNameFactory());

    // Server/Ipc (typed messages protocol)
    private IReadOnlyList<IProtocolHandler> _protocolHandlers;
    public IReadOnlyList<IProtocolHandler> ProtocolHandlers => _protocolHandlers ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(ProtocolHandlers),
      () => new IProtocolHandler[] {
        Create(new EchoProtocolHandler()),
        Create(new HelloProtocolHandler()),
        Create(new TypedMessageProtocolHandler(TypedMessageRequestHandlers)),
      });

    private ITypedEventForwarder _typedEventForwarder;
    public ITypedEventForwarder TypedEventForwarder => _typedEventForwarder ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(TypedEventForwarder),
      () => new TypedEventForwarder(TypedEventSender, IndexingServer, FileSystemSnapshotManager, SearchEngine));

    private ITypedEventSender _typedEventSender;
    public ITypedEventSender TypedEventSender => _typedEventSender ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(TypedEventSender),
      () => new TypedEventSender(IpcResponseQueue, IpcRequestIdFactory));

    private IReadOnlyList<ITypedMessageRequestHandler> _typedMessageRequestHandlers;
    public IReadOnlyList<ITypedMessageRequestHandler> TypedMessageRequestHandlers => _typedMessageRequestHandlers ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(TypedMessageRequestHandlers),
      () => new ITypedMessageRequestHandler[] {
        Create(new GetDatabaseDetailsRequestHandler(FileSystemSnapshotManager, SearchEngine)),
        Create(new GetDatabaseStatisticsRequestHandler(FileSystemSnapshotManager, SearchEngine, IndexingServer)),
        Create(new GetDirectoryDetailsRequestHandler(FileSystemSnapshotManager, SearchEngine)),
        Create(new GetDirectoryEntriesMultipleRequestHandler(FileSystemSnapshotManager)),
        Create(new GetDirectoryEntriesRequestHandler(FileSystemSnapshotManager)),
        Create(new GetDirectoryStatisticsRequestHandler(FileSystemSnapshotManager)),
        Create(new GetFileExtractsRequestHandler(SearchEngine)),
        Create(new GetFileSystemRequestHandler(FileSystemSnapshotManager)),
        Create(new GetFileSystemTreeRequestHandler(FileSystemSnapshotManager)),
        Create(new GetFileSystemVersionRequestHandler(FileSystemSnapshotManager)),
        Create(new GetProjectDetailsRequestHandler(FileSystemSnapshotManager, SearchEngine)),
        Create(new GetServerStatisticsRequestHandler(FileSystemSnapshotManager, SearchEngine)),
        Create(new PauseIndexingRequestHandler(IndexingServer)),
        Create(new RefreshFileSystemTreeRequestHandler(IndexingServer)),
        Create(new RegisterFileRequestHandler(FileRegistrationTracker)),
        Create(new ResumeIndexingRequestHandler(IndexingServer)),
        Create(new SearchCodeRequestHandler(SearchEngine, FileSystemNameFactory)),
        Create(new SearchFilePathsRequestHandler(SearchEngine, FileSystemNameFactory)),
        Create(new SetProjectOptionsRequestHandler(SearchEngine)),
        Create(new UnregisterFileRequestHandler(FileRegistrationTracker)),
      });

    private IIpcRequestDispatcher _ipcRequestDispatcher;
    public IIpcRequestDispatcher IpcRequestDispatcher => _ipcRequestDispatcher ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(IpcRequestDispatcher),
      () => new IpcRequestDispatcher(CustomThreadPool, TaskQueueFactory, IpcResponseQueue, ProtocolHandlers));

    private IIpcResponseQueue _ipcResponseQueue;
    public IIpcResponseQueue IpcResponseQueue => _ipcResponseQueue ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(IpcResponseQueue),
      () => new IpcResponseQueue());

    // Server/JsonRpc (JSON-RPC protocol)
    private JsonRpcServer _jsonRpcServer;
    public JsonRpcServer JsonRpcServer => _jsonRpcServer ??= CreateFor(ServerProtocolMode.JsonRpc, nameof(JsonRpcServer),
      () => new JsonRpcServer(JsonRpcOutput, JsonRpcConnection, JsonRpcEventForwarder));

    private JsonRpcConnection _jsonRpcConnection;
    public JsonRpcConnection JsonRpcConnection => _jsonRpcConnection ??= CreateFor(ServerProtocolMode.JsonRpc, nameof(JsonRpcConnection),
      () => new JsonRpcConnection(ProtocolRequestHandler, JsonRpcOutput));

    private JsonRpcOutput _jsonRpcOutput;
    public JsonRpcOutput JsonRpcOutput => _jsonRpcOutput ??= CreateFor(ServerProtocolMode.JsonRpc, nameof(JsonRpcOutput),
      () => new JsonRpcOutput());

    private IProtocolRequestHandler _protocolRequestHandler;
    public IProtocolRequestHandler ProtocolRequestHandler => _protocolRequestHandler ??= CreateFor(ServerProtocolMode.JsonRpc, nameof(ProtocolRequestHandler),
      () => new ProtocolRequestHandler(FileSystemSnapshotManager, SearchEngine, FileRegistrationTracker, IndexingServer));

    private JsonRpcNotificationSender _jsonRpcNotificationSender;
    public JsonRpcNotificationSender JsonRpcNotificationSender => _jsonRpcNotificationSender ??= CreateFor(ServerProtocolMode.JsonRpc, nameof(JsonRpcNotificationSender),
      () => new JsonRpcNotificationSender(JsonRpcOutput));

    private JsonRpcEventForwarder _jsonRpcEventForwarder;
    public JsonRpcEventForwarder JsonRpcEventForwarder => _jsonRpcEventForwarder ??= CreateFor(ServerProtocolMode.JsonRpc, nameof(JsonRpcEventForwarder),
      () => new JsonRpcEventForwarder(JsonRpcNotificationSender, IndexingServer, FileSystemSnapshotManager, SearchEngine));

    // Server/Operations
    private IOperationIdFactory _operationIdFactory;
    public IOperationIdFactory OperationIdFactory => _operationIdFactory ??= Create(new OperationIdFactory());

    private IOperationProcessor _operationProcessor;
    public IOperationProcessor OperationProcessor => _operationProcessor ??= Create(new OperationProcessor(OperationIdFactory));

    // Server/ProgressTracking
    private IProgressEventSender _progressEventSender;
    public IProgressEventSender ProgressEventSender => _progressEventSender ??= _mode == ServerProtocolMode.JsonRpc
      ? JsonRpcNotificationSender
      : Create(new TypedEventProgressSender(TypedEventSender));

    private IProgressTrackerFactory _progressTrackerFactory;
    public IProgressTrackerFactory ProgressTrackerFactory => _progressTrackerFactory ??= Create(new ProgressTrackerFactory(ProgressEventSender));

    // Server/Projects
    private IReadOnlyList<IProjectDiscoveryProvider> _projectDiscoveryProviders;
    public IReadOnlyList<IProjectDiscoveryProvider> ProjectDiscoveryProviders => _projectDiscoveryProviders ??= new IProjectDiscoveryProvider[] {
      Create(new ChromiumProjectDiscoveryProvider(ConfigurationFileLocator, FileSystem)),
      Create(new ProjectFileDiscoveryProvider(FileSystem)),
    };

    private IProjectDiscovery _projectDiscovery;
    public IProjectDiscovery ProjectDiscovery => _projectDiscovery ??= Create(new ProjectDiscovery(ProjectDiscoveryProviders));

    // Server/Search
    private ICompiledTextSearchDataFactory _compiledTextSearchDataFactory;
    public ICompiledTextSearchDataFactory CompiledTextSearchDataFactory => _compiledTextSearchDataFactory ??= Create(new CompiledTextSearchDataFactory(SearchStringParser, CompiledTextSearchProviderFactory));

    private ICompiledTextSearchProviderFactory _compiledTextSearchProviderFactory;
    public ICompiledTextSearchProviderFactory CompiledTextSearchProviderFactory => _compiledTextSearchProviderFactory ??= Create(new CompiledTextSearchProviderFactory());

    private ISearchEngine _searchEngine;
    public ISearchEngine SearchEngine => _searchEngine ??= Create(new SearchEngine(
      FileSystemSnapshotManager, FileSystemNameFactory, LongRunningFileSystemTaskQueue, FileDatabaseSnapshotFactory, ProjectDiscovery,
      CompiledTextSearchDataFactory, OperationProcessor));

    private ISearchStringParser _searchStringParser;
    public ISearchStringParser SearchStringParser => _searchStringParser ??= Create(new SearchStringParser());

    // Server/Threads
    private ICustomThreadPool _customThreadPool;
    public ICustomThreadPool CustomThreadPool => _customThreadPool ??= Create(new CustomThreadPool(DateTimeProvider));

    private ILongRunningFileSystemTaskQueue _longRunningFileSystemTaskQueue;
    public ILongRunningFileSystemTaskQueue LongRunningFileSystemTaskQueue => _longRunningFileSystemTaskQueue ??= Create(new LongRunningFileSystemTaskQueue(TaskQueueFactory));

    private IReceiveRequestsThread _receiveRequestsThread;
    public IReceiveRequestsThread ReceiveRequestsThread => _receiveRequestsThread ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(ReceiveRequestsThread),
      () => new ReceiveRequestsThread(IpcRequestDispatcher));

    private ISendResponsesThread _sendResponsesThread;
    public ISendResponsesThread SendResponsesThread => _sendResponsesThread ??= CreateFor(ServerProtocolMode.TypedMessages, nameof(SendResponsesThread),
      () => new SendResponsesThread(IpcResponseQueue));

    private ITaskQueueFactory _taskQueueFactory;
    public ITaskQueueFactory TaskQueueFactory => _taskQueueFactory ??= Create(new TaskQueueFactory(CustomThreadPool, DateTimeProvider));
  }
}
