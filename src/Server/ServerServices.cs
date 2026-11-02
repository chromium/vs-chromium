// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace VsChromium.Server {
  /// <summary>
  /// The composition root of the server: the list of all the components (services) of the server,
  /// all singletons, created on demand with constructor injection (replaces MEF, so that the list is
  /// explicit and no reflection based discovery is needed, e.g. for NativeAOT). Components consuming
  /// all the implementations of an interface (e.g. the request handlers) get an
  /// <c>IEnumerable&lt;T&gt;</c>. <c>TestServerServices</c> (Server.Tests) checks that all the services
  /// can be created.
  /// </summary>
  public static class ServerServices {
    /// <summary>
    /// Creates the service provider of the server, with optional overrides of some services (tests).
    /// </summary>
    public static ServiceProvider CreateServiceProvider(Action<IServiceCollection> overrides = null) {
      var services = new ServiceCollection();
      AddServerServices(services);
      overrides?.Invoke(services);
      return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public static void AddServerServices(IServiceCollection services) {
      // Core/Configuration
      services.AddSingleton<VsChromium.Core.Configuration.IConfigurationFileLocator, VsChromium.Core.Configuration.ConfigurationFileLocator>();

      // Core/Ipc
      services.AddSingleton<VsChromium.Core.Ipc.IIpcRequestIdFactory, VsChromium.Core.Ipc.IpcRequestIdFactory>();

      // Core/Threads
      services.AddSingleton<VsChromium.Core.Threads.IDateTimeProvider, VsChromium.Core.Threads.DateTimeProvider>();

      // Core.Server/Files
      services.AddSingleton<VsChromium.Core.Files.IDirectoryChangeWatcherFactory, VsChromium.Core.Files.DirectoryChangeWatcherFactory>();
      services.AddSingleton<VsChromium.Core.Files.IFileSystem, VsChromium.Core.Files.PortableFileSystem>();

      // Server/FileSystem
      services.AddSingleton<VsChromium.Server.FileSystem.IFileSystemSnapshotBuilder, VsChromium.Server.FileSystem.Builder.FileSystemSnapshotBuilder>();
      services.AddSingleton<VsChromium.Server.FileSystem.IFileRegistrationTracker, VsChromium.Server.FileSystem.FileRegistrationTracker>();
      services.AddSingleton<VsChromium.Server.FileSystem.IFileSystemSnapshotManager, VsChromium.Server.FileSystem.FileSystemSnapshotManager>();

      // Server/FileSystemContents
      services.AddSingleton<VsChromium.Server.FileSystemContents.IFileContentsFactory, VsChromium.Server.FileSystemContents.FileContentsFactory>();

      // Server/FileSystemDatabase
      services.AddSingleton<VsChromium.Server.FileSystemDatabase.IFileDatabaseSnapshotFactory, VsChromium.Server.FileSystemDatabase.FileDatabaseSnapshotFactory>();

      // Server/FileSystemNames
      services.AddSingleton<VsChromium.Server.FileSystemNames.IFileSystemNameFactory, VsChromium.Server.FileSystemNames.FileSystemNameFactory>();

      // Server/Ipc
      services.AddSingleton<VsChromium.Server.Ipc.ProtocolHandlers.IProtocolHandler, VsChromium.Server.Ipc.ProtocolHandlers.EchoProtocolHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.ProtocolHandlers.IProtocolHandler, VsChromium.Server.Ipc.ProtocolHandlers.HelloProtocolHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.ProtocolHandlers.IProtocolHandler, VsChromium.Server.Ipc.ProtocolHandlers.TypedMessageProtocolHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedEvents.ITypedEventForwarder, VsChromium.Server.Ipc.TypedEvents.TypedEventForwarder>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedEvents.ITypedEventSender, VsChromium.Server.Ipc.TypedEvents.TypedEventSender>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetDatabaseDetailsRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetDatabaseStatisticsRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetDirectoryDetailsRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetDirectoryEntriesMultipleRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetDirectoryEntriesRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetDirectoryStatisticsRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetFileExtractsRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetFileSystemRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetFileSystemTreeRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetFileSystemVersionRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetProjectDetailsRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.GetServerStatisticsRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.PauseIndexingRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.RefreshFileSystemTreeRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.RegisterFileRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.ResumeIndexingRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.SearchCodeRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.SearchFilePathsRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.TypedMessageHandlers.ITypedMessageRequestHandler, VsChromium.Server.Ipc.TypedMessageHandlers.UnregisterFileRequestHandler>();
      services.AddSingleton<VsChromium.Server.Ipc.IIpcRequestDispatcher, VsChromium.Server.Ipc.IpcRequestDispatcher>();
      services.AddSingleton<VsChromium.Server.Ipc.IIpcResponseQueue, VsChromium.Server.Ipc.IpcResponseQueue>();

      // Server/Operations
      services.AddSingleton<VsChromium.Server.Operations.IOperationIdFactory, VsChromium.Server.Operations.OperationIdFactory>();
      services.AddSingleton<VsChromium.Server.Operations.IOperationProcessor, VsChromium.Server.Operations.OperationProcessor>();

      // Server/ProgressTracking
      services.AddSingleton<VsChromium.Server.ProgressTracking.IProgressTrackerFactory, VsChromium.Server.ProgressTracking.ProgressTrackerFactory>();

      // Server/Projects
      services.AddSingleton<VsChromium.Server.Projects.IProjectDiscoveryProvider, VsChromium.Server.Projects.Chromium.ChromiumProjectDiscoveryProvider>();
      services.AddSingleton<VsChromium.Server.Projects.IProjectDiscoveryProvider, VsChromium.Server.Projects.ProjectFile.ProjectFileDiscoveryProvider>();
      services.AddSingleton<VsChromium.Server.Projects.IProjectDiscovery, VsChromium.Server.Projects.ProjectDiscovery>();

      // Server/Search
      services.AddSingleton<VsChromium.Server.Search.ICompiledTextSearchDataFactory, VsChromium.Server.Search.CompiledTextSearchDataFactory>();
      services.AddSingleton<VsChromium.Server.Search.ICompiledTextSearchProviderFactory, VsChromium.Server.Search.CompiledTextSearchProviderFactory>();
      services.AddSingleton<VsChromium.Server.Search.ISearchEngine, VsChromium.Server.Search.SearchEngine>();
      services.AddSingleton<VsChromium.Server.Search.ISearchStringParser, VsChromium.Server.Search.SearchStringParser>();

      // Server/Threads
      services.AddSingleton<VsChromium.Server.Threads.ICustomThreadPool>(provider => new VsChromium.Server.Threads.CustomThreadPool(provider.GetRequiredService<VsChromium.Core.Threads.IDateTimeProvider>()));
      services.AddSingleton<VsChromium.Server.Threads.ILongRunningFileSystemTaskQueue, VsChromium.Server.Threads.LongRunningFileSystemTaskQueue>();
      services.AddSingleton<VsChromium.Server.Threads.IReceiveRequestsThread, VsChromium.Server.Threads.ReceiveRequestsThread>();
      services.AddSingleton<VsChromium.Server.Threads.ISendResponsesThread, VsChromium.Server.Threads.SendResponsesThread>();
      services.AddSingleton<VsChromium.Server.Threads.ITaskQueueFactory, VsChromium.Server.Threads.TaskQueueFactory>();

      // Server
      services.AddSingleton<VsChromium.Server.IIndexingServer, VsChromium.Server.IndexingServer>();
      services.AddSingleton<VsChromium.Server.IServer, VsChromium.Server.IpcServer>();
    }
  }
}
