// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.Diagnostics;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetServerStatisticsRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public GetServerStatisticsRequestHandler(IFileSystemSnapshotManager snapshotManager, ISearchEngine searchEngine) {
      _snapshotManager = snapshotManager;
      _searchEngine = searchEngine;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetServerStatisticsRequest)typedRequest;
      if (request.ForceGarbageCollection) {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
      }

      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      var snapshot = _snapshotManager.CurrentSnapshot;
      using (var process = System.Diagnostics.Process.GetCurrentProcess()) {
        return new GetServerStatisticsResponse {
          RuntimeDescription = RuntimeInformation.FrameworkDescription + " " + RuntimeInformation.ProcessArchitecture +
                               " (" + RuntimeInformation.OSDescription + ")",
          ProcessId = process.Id,
          ProcessorCount = Environment.ProcessorCount,
          UptimeMs = (DateTime.Now - process.StartTime).TotalMilliseconds,
          CpuTotalMs = process.TotalProcessorTime.TotalMilliseconds,
          CpuUserMs = process.UserProcessorTime.TotalMilliseconds,
          CpuKernelMs = process.PrivilegedProcessorTime.TotalMilliseconds,
          WorkingSetBytes = process.WorkingSet64,
          PeakWorkingSetBytes = process.PeakWorkingSet64,
          PrivateBytes = process.PrivateMemorySize64,
          ThreadCount = process.Threads.Count,
          HandleCount = process.HandleCount,

          GcHeapBytes = GC.GetTotalMemory(false),
          GcTotalAllocatedBytes = GetTotalAllocatedBytes(),
          GcGen0Collections = GC.CollectionCount(0),
          GcGen1Collections = GC.CollectionCount(1),
          GcGen2Collections = GC.CollectionCount(2),
          GcTotalPauseMs = GetTotalPauseMs(),

          NativeAllocatedBytes = HeapAllocStatic.TotalMemory,
          IndexedFileContentsBytes = database.TotalFileContentsLength,
          FileCount = database.FileNameCount,
          SearchableFileCount = database.SearchableFileCount,
          ProjectCount = snapshot.ProjectRoots.Count,

          OperationTimings = OperationTimings.GetSnapshot(),
        };
      }
    }

    private static long GetTotalAllocatedBytes() {
#if NETCOREAPP
      return GC.GetTotalAllocatedBytes(precise: false);
#else
      return -1;
#endif
    }

    private static double GetTotalPauseMs() {
#if NET7_0_OR_GREATER
      return GC.GetTotalPauseDuration().TotalMilliseconds;
#else
      return -1;
#endif
    }
  }
}
