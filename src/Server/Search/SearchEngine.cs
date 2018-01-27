// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Files.PatternMatching;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Operations;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.Search {
  [Export(typeof(ISearchEngine))]
  public class SearchEngine : ISearchEngine {
    private static readonly TaskId UpdateFileContentsTaskId = new TaskId("UpdateFileContentsTaskId");
    private static readonly TaskId ComputeNewStatedId = new TaskId("ComputeNewStateId");
    private static readonly TaskId GarbageCollectId = new TaskId("GarbageCollectId");
    private readonly IFileDatabaseSnapshotFactory _fileDatabaseSnapshotFactory;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly ICompiledTextSearchDataFactory _compiledTextSearchDataFactory;
    private readonly IOperationProcessor _operationProcessor;
    private readonly TaskCancellation _taskCancellation = new TaskCancellation();

    /// <summary>
    /// We use a <see cref="ITaskQueue"/> to ensure that we process all events
    /// in sequence (asynchronously). This ensure the final state of <see
    /// cref="_currentFileDatabaseSnapshot"/> reflects the state we should keep wrt to
    /// the last event reveived.
    /// </summary>
    private readonly ITaskQueue _taskQueue;

    private volatile IFileDatabaseSnapshot _currentFileDatabaseSnapshot;
    private long _currentTreeVersion = -1;
    private bool _allowIncrementalUpdates;

    [ImportingConstructor]
    public SearchEngine(
      IFileSystemSnapshotManager fileSystemSnapshotManager,
      IFileSystemNameFactory fileSystemNameFactory,
      ILongRunningFileSystemTaskQueue taskQueue,
      IFileDatabaseSnapshotFactory fileDatabaseSnapshotFactory,
      IProjectDiscovery projectDiscovery,
      ICompiledTextSearchDataFactory compiledTextSearchDataFactory,
      IOperationProcessor operationProcessor) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _taskQueue = taskQueue;
      _fileDatabaseSnapshotFactory = fileDatabaseSnapshotFactory;
      _projectDiscovery = projectDiscovery;
      _compiledTextSearchDataFactory = compiledTextSearchDataFactory;
      _operationProcessor = operationProcessor;

      // Create a "Null" state
      _currentFileDatabaseSnapshot = _fileDatabaseSnapshotFactory.CreateEmpty();

      // Setup computing a new state everytime a new tree is computed.
      fileSystemSnapshotManager.SnapshotScanFinished += FileSystemSnapshotManagerOnSnapshotScanFinished;
      fileSystemSnapshotManager.FilesChanged += FileSystemSnapshotManagerOnFilesChanged;
    }

    public IFileDatabaseSnapshot CurrentFileDatabaseSnapshot {
      get { return _currentFileDatabaseSnapshot; }
    }

    public SearchFilePathsResult SearchFilePaths(SearchParams searchParams) {
      // taskCancellation is used to make sure we cancel previous tasks as fast
      // as possible to avoid using too many CPU resources if the caller keeps
      // asking us to search for things. Note that this assumes the caller is
      // only interested in the result of the *last* query, while the previous
      // queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();

      var preProcessResult = PreProcessFileSystemNameSearch<FileName>(
        searchParams,
        MatchFileName,
        MatchFileRelativePath);
      if (preProcessResult == null)
        return SearchFilePathsResult.Empty;

      using (preProcessResult) {
        var searchedFileCount = 0;
        var matches = _currentFileDatabaseSnapshot.FileNames
          .AsParallel()
          // We need the line below because of "Take" (.net 4.0 PLinq
          // limitation)
          .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
          .WithCancellation(_taskCancellation.GetNewToken())
          .Where(
            item => {
              if (!searchParams.IncludeSymLinks) {
                if (_currentFileDatabaseSnapshot.IsContainedInSymLink(item))
                  return false;
              }
              Interlocked.Increment(ref searchedFileCount);
              return preProcessResult.Matcher(item);
            })
          .Take(searchParams.MaxResults)
          .ToList();

        return new SearchFilePathsResult {
          FileNames = matches,
          TotalCount = searchedFileCount
        };
      }
    }

    public SearchCodeResult SearchCode(SearchParams searchParams) {
      // taskCancellation is used to make sure we cancel previous tasks as fast
      // as possible to avoid using too many CPU resources if the caller keeps
      // asking us to search for things. Note that this assumes the caller is
      // only interested in the result of the *last* query, while the previous
      // queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();

      // Setup the file name/path filter.
      Func<FileName, bool> fileNameMatcher = x => true;
      if (!string.IsNullOrEmpty(searchParams.FilePathPattern)) {
        // Don't include match case, etc.
        var tempSearch = new SearchParams {
          SearchString = searchParams.FilePathPattern,
          IncludeSymLinks = true
        };
        var preProcessResult = PreProcessFileSystemNameSearch<FileName>(
          tempSearch, MatchFileName, MatchFileRelativePath);
        if (preProcessResult != null) {
          fileNameMatcher = preProcessResult.Matcher;
        }
      }

      // Perform the text search
      using (var searchContentsData = _compiledTextSearchDataFactory.Create(searchParams, fileNameMatcher)) {
        var cancellationToken = _taskCancellation.GetNewToken();
        return SearchCodeWorker(
          searchContentsData,
          searchParams.MaxResults,
          searchParams.IncludeSymLinks,
          cancellationToken);
      }
    }

    private SearchCodeResult SearchCodeWorker(
      CompiledTextSearchData compiledTextSearchData,
      int maxResults,
      bool includeSymLinks,
      CancellationToken cancellationToken) {
      var progressTracker = new OperationProgressTracker(maxResults, cancellationToken);
      var searchedFileIds = new PartitionedBitArray(
        _currentFileDatabaseSnapshot.SearchableFileCount,
        Environment.ProcessorCount * 2);
      var matches = _currentFileDatabaseSnapshot.FileContentsPieces
        .AsParallel()
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .WithCancellation(cancellationToken)
        .Where(x => !progressTracker.ShouldEndProcessing)
        .Select(item => {
          // Filter out files inside symlinks if needed
          if (!includeSymLinks) {
            if (_currentFileDatabaseSnapshot.IsContainedInSymLink(item.FileName))
              return default(SearchableContentsResult);
          }
          // Filter out files that don't match the file name match pattern
          if (!compiledTextSearchData.FileNameFilter(item.FileName)) {
            return default(SearchableContentsResult);
          }
          searchedFileIds.Set(item.FileId, true);
          return new SearchableContentsResult {
            FileContentsPiece = item,
            Spans = item
              .FindAll(compiledTextSearchData, progressTracker)
              .Select(x => new FilePositionSpan {
                Position = x.Position,
                Length = x.Length,
              })
              .ToList(),
          };
        })
        .Where(r => r.Spans != null && r.Spans.Count > 0)
        .GroupBy(r => r.FileContentsPiece.FileId)
        .Select(g => new FileSearchResult {
          FileName = g.First().FileContentsPiece.FileName,
          Spans = g.OrderBy(x => x.Spans.First().Position).SelectMany(x => x.Spans).ToList()
        })
        .ToList();

      return new SearchCodeResult {
        Entries = matches,
        SearchedFileCount = searchedFileIds.Count,
        TotalFileCount = _currentFileDatabaseSnapshot.SearchableFileCount,
        HitCount = progressTracker.ResultCount,
      };
    }

    private struct SearchableContentsResult {
      public IFileContentsPiece FileContentsPiece { get; set; }
      public List<FilePositionSpan> Spans { get; set; }
    }

    public IEnumerable<FileExtract> GetFileExtracts(FullPath path, IEnumerable<FilePositionSpan> spans, int maxLength) {
      var filename = _fileSystemNameFactory.CreateProjectFileFromFullPath(_projectDiscovery, path);
      if (filename.IsNull)
        return Enumerable.Empty<FileExtract>();

      return _currentFileDatabaseSnapshot.GetFileExtracts(filename.FileName, spans, maxLength);
    }

    public event EventHandler<OperationInfo> FilesLoading;

    public event EventHandler<OperationInfo> FilesLoadingProgress;

    public event EventHandler<FilesLoadedResult> FilesLoaded;

    protected virtual void OnFilesLoading(OperationInfo e) {
      EventHandler<OperationInfo> handler = FilesLoading;
      if (handler != null) handler(this, e);
    }

    protected virtual void OnFilesLoadingProgress(OperationInfo e) {
      EventHandler<OperationInfo> handler = FilesLoadingProgress;
      if (handler != null) handler(this, e);
    }

    protected virtual void OnFilesLoaded(FilesLoadedResult e) {
      EventHandler<FilesLoadedResult> handler = FilesLoaded;
      if (handler != null) handler(this, e);
    }

    private void FileSystemSnapshotManagerOnFilesChanged(object sender, FilesChangedEventArgs filesChangedEventArgs) {
      _taskQueue.Enqueue(UpdateFileContentsTaskId, cancellationToken => {
        UpdateFileContentsLongTask(filesChangedEventArgs, cancellationToken);
      });
    }

    private void FileSystemSnapshotManagerOnSnapshotScanFinished(object sender, SnapshotScanResult e) {
      if (e.Error != null)
        return;

      _taskQueue.Enqueue(ComputeNewStatedId, cancellationToken =>
        ComputeNewStateLongTask(e.NewSnapshot, e.FullPathChanges, cancellationToken));

      // Enqueue a GC at this point makes sense as there might be a lot of
      // garbage to reclaim from previous file contents stored in native heap.
      // By performin a full GC and waiting for finalizer, we ensure that (most)
      // orphan SafeHandles are released in a timely fashion. We enqueue a
      // separate task to ensure there is no potential state keeping these
      // variables alive for slightly too long.
      _taskQueue.Enqueue(GarbageCollectId, cancellationToken => {
        Logger.LogMemoryStats();
        GC.Collect(GC.MaxGeneration);
        GC.WaitForPendingFinalizers();
        Logger.LogMemoryStats();
      });
    }

    private class SearchPreProcessResult<T> : IDisposable {
      public Func<T, bool> Matcher { get; set; }
      public CompiledTextSearchData SearchData { get; set; }

      public void Dispose() {
        if (SearchData != null) {
          SearchData.Dispose();
          SearchData = null;
        }
      }
    }

    private SearchPreProcessResult<T> PreProcessFileSystemNameSearch<T>(SearchParams searchParams,
      Func<IPathMatcher, T, IPathComparer, bool> matchName,
      Func<IPathMatcher, T, IPathComparer, bool> matchRelativeName) {

      // Regex has its own set of rules for pre-processing
      if (searchParams.Regex) {
        return PreProcessFileSystemNameRegularExpressionSearch(
          searchParams,
          matchName,
          matchRelativeName);
      }

      // Check pattern is not empty
      var pattern = (searchParams.SearchString ?? "").Trim();
      if (string.IsNullOrWhiteSpace(pattern))
        return null;

      // Split pattern around ";", normalize directory separators and
      // add "*" if not a whole word search
      var patterns = pattern
        .Split(new[] { ';' })
        .Where(x => !string.IsNullOrWhiteSpace(x.Trim()))
        .Select(x => x.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar))
        .Select(x => {
          // Exception to ".gitignore" syntax: If the search string doesn't contain any special
          // character, surround the pattern with "*" so that we match sub-strings.
          // TODO(rpaquay): What about "."? Special or not?
          if (x.IndexOf(Path.DirectorySeparatorChar) < 0 && x.IndexOf('*') < 0) {
            if (!searchParams.MatchWholeWord) {
              x = "*" + x + "*";
            }
          }
          return x;
        })
        .ToReadOnlyCollection();

      var matcher = new AnyPathMatcher(patterns.Select(PatternParser.ParsePattern));

      var comparer = searchParams.MatchCase ? PathComparerRegistry.CaseSensitive : PathComparerRegistry.CaseInsensitive;
      if (patterns.Any(x => x.Contains(Path.DirectorySeparatorChar))) {
        return new SearchPreProcessResult<T> {
          Matcher = (item) => matchRelativeName(matcher, item, comparer)
        };
      } else {
        return new SearchPreProcessResult<T> {
          Matcher = (item) => matchName(matcher, item, comparer)
        };
      }
    }

    private SearchPreProcessResult<T> PreProcessFileSystemNameRegularExpressionSearch<T>(SearchParams searchParams,
      Func<IPathMatcher, T, IPathComparer, bool> matchName,
      Func<IPathMatcher, T, IPathComparer, bool> matchRelativeName) {

      var pattern = searchParams.SearchString ?? "";
      pattern = pattern.Trim();
      if (string.IsNullOrWhiteSpace(pattern))
        return null;

      var data = _compiledTextSearchDataFactory.Create(searchParams, x => true);
      var provider = data.GetSearchContainer(data.ParsedSearchString.MainEntry);
      var matcher = new CompiledTextSearchProviderPathMatcher(provider);
      var comparer = searchParams.MatchCase ? PathComparerRegistry.CaseSensitive : PathComparerRegistry.CaseInsensitive;
      if (pattern.Contains('/') || pattern.Contains("\\\\")) {
        return new SearchPreProcessResult<T> {
          Matcher = (item) => matchRelativeName(matcher, item, comparer),
          SearchData = data,
        };
      } else {
        return new SearchPreProcessResult<T> {
          Matcher = (item) => matchName(matcher, item, comparer),
          SearchData = data,
        };
      }
    }

    public class NonePathMatcher : IPathMatcher {
      public bool MatchDirectoryName(RelativePath path, IPathComparer comparer) {
        return false;
      }

      public bool MatchFileName(RelativePath path, IPathComparer comparer) {
        return false;
      }
    }

    public class CompiledTextSearchProviderPathMatcher : IPathMatcher {
      private readonly ICompiledTextSearchContainer _searchContainer;

      public CompiledTextSearchProviderPathMatcher(ICompiledTextSearchContainer searchContainer) {
        _searchContainer = searchContainer;
      }

      public bool MatchDirectoryName(RelativePath path, IPathComparer comparer) {
        return MatchRelativePath(path);
      }

      public bool MatchFileName(RelativePath path, IPathComparer comparer) {
        return MatchRelativePath(path);
      }

      private unsafe bool MatchRelativePath(RelativePath path) {
        var value = path.Value;
        var hasDirectorySeparators = value.IndexOf(Path.DirectorySeparatorChar) >= 0;
        var ptr = Marshal.StringToHGlobalAnsi(value);
        try {
          var range = new TextFragment(ptr, 0, value.Length, sizeof(byte));

          var hit = _searchContainer.GetAsciiSearch()
            .FindFirst(range, OperationProgressTracker.None);
          if (hit.HasValue)
            return true;

          if (!hasDirectorySeparators)
            return false;

          // Replace '\' with '/' and try again
          var first = (byte*)ptr.ToPointer();
          var last = first + value.Length;
          for (; first != last; first++) {
            if ((char)(*first) == Path.DirectorySeparatorChar) {
              *first = (byte)Path.AltDirectorySeparatorChar;
            }
          }

          // Search again
          hit = _searchContainer.GetAsciiSearch()
            .FindFirst(range, OperationProgressTracker.None);
          return hit.HasValue;
        } finally {
          Marshal.FreeHGlobal(ptr);
        }
      }
    }

    private void ComputeNewStateLongTask(FileSystemSnapshot newSnapshot, FullPathChanges fullPathChanges, CancellationToken cancellationToken) {
      UpdateFileDatabaseLongTask(newSnapshot, options => options.AllowIncrementalUpdates
        ? CreateIncremental(newSnapshot, fullPathChanges, options, cancellationToken)
        : CreateFullScan(newSnapshot, options, cancellationToken), cancellationToken);
    }

    private void UpdateFileContentsLongTask(FilesChangedEventArgs args, CancellationToken cancellationToken) {
      UpdateFileDatabaseLongTask(args.FileSystemSnapshot, options => options.AllowIncrementalUpdates
        ? CreateWithChangedFiles(args, options, cancellationToken)
        : CreateFullScan(args.FileSystemSnapshot, options, cancellationToken), cancellationToken);
    }

    private IFileDatabaseSnapshot CreateFullScan(FileSystemSnapshot newSnapshot,
      UpdateFileDatabaseOptions options, CancellationToken cancellationToken) {
      return CreateIncremental(newSnapshot, null, options, cancellationToken);
    }

    private IFileDatabaseSnapshot CreateIncremental(FileSystemSnapshot newSnapshot, FullPathChanges fullPathChanges, UpdateFileDatabaseOptions options, CancellationToken cancellationToken) {
      return _fileDatabaseSnapshotFactory.CreateIncremental(
        options.CurrentFileDatabase,
        newSnapshot,
        fullPathChanges,
        onIntermadiateResult: fileDatabase => {
          // Store and activate intermediate new state (atomic operation).
          _currentFileDatabaseSnapshot = fileDatabase;
          OnFilesLoadingProgress(options.OperationInfo);
        },
        onLoading: () => OnFilesLoading(options.OperationInfo),
        onLoaded: () => OnFilesLoaded(new FilesLoadedResult {
          OperationInfo = options.OperationInfo,
          TreeVersion = _currentTreeVersion,
        }),
        cancellationToken: cancellationToken);
    }

    private IFileDatabaseSnapshot CreateWithChangedFiles(FilesChangedEventArgs args, UpdateFileDatabaseOptions options,
      CancellationToken cancellationToken) {
      return _fileDatabaseSnapshotFactory.CreateWithChangedFiles(
        options.CurrentFileDatabase,
        args.FileSystemSnapshot,
        args.ChangedFiles,
        onIntermadiateResult: fileDatabase => {
          // Store and activate intermediate new state (atomic operation).
          _currentFileDatabaseSnapshot = fileDatabase;
          OnFilesLoadingProgress(options.OperationInfo);
        },
        onLoading: () => OnFilesLoading(options.OperationInfo),
        onLoaded: () => OnFilesLoaded(new FilesLoadedResult {
          OperationInfo = options.OperationInfo,
          TreeVersion = _currentTreeVersion,
        }),
        cancellationToken: cancellationToken);
    }

    private class UpdateFileDatabaseOptions {
      public OperationInfo OperationInfo { get; set; }
      public bool AllowIncrementalUpdates { get; set; }
      public IFileDatabaseSnapshot CurrentFileDatabase { get; set; }
    }

    private void UpdateFileDatabaseLongTask(FileSystemSnapshot fileSystemSnapshot,
      Func<UpdateFileDatabaseOptions, IFileDatabaseSnapshot> updater, CancellationToken cancellationToken) {

      var options = new UpdateFileDatabaseOptions();

      _operationProcessor.Execute(new OperationHandlers {
        OnBeforeExecute = info => {
          options.OperationInfo = info;
          options.AllowIncrementalUpdates = _allowIncrementalUpdates;
          _allowIncrementalUpdates = false;
        },

        OnError = (info, error) => {
          _currentTreeVersion = fileSystemSnapshot.Version;
          OnFilesLoading(info);
          OnFilesLoaded(new FilesLoadedResult {
            OperationInfo = info,
            Error = error,
            TreeVersion = _currentTreeVersion,
          });
        },

        Execute = info => {
          using (new TimeElapsedLogger(
            $"Computing new state of file database (allow incremental={options.AllowIncrementalUpdates}",
            cancellationToken)) {
            options.CurrentFileDatabase = _currentFileDatabaseSnapshot;
            var newState = updater(options);
            _currentFileDatabaseSnapshot = newState;
            _currentTreeVersion = fileSystemSnapshot.Version;
            _allowIncrementalUpdates = true; // Success => we allow incremtal updates next time
          }

          OnFilesLoaded(new FilesLoadedResult {
            OperationInfo = info,
            TreeVersion = fileSystemSnapshot.Version
          });
        }
      });
    }

    private bool MatchFileName(IPathMatcher matcher, FileName fileName, IPathComparer comparer) {
      return matcher.MatchFileName(new RelativePath(fileName.Name), comparer);
    }

    private bool MatchFileRelativePath(IPathMatcher matcher, FileName fileName, IPathComparer comparer) {
      return matcher.MatchFileName(fileName.RelativePath, comparer);
    }
  }
}