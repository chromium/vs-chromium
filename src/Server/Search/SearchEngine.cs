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
    /// cref="_currentFileDatabase"/> reflects the state we should keep wrt to
    /// the last event reveived.
    /// </summary>
    private readonly ITaskQueue _taskQueue;

    /// <summary>
    /// This is just for asserting that code runs only in tasks serialized from <see cref="_taskQueue"/>.
    /// </summary>
    private bool _inTaskQueueTask;

    /// <summary>
    /// The currently active file database snapshot. A new one may be computed in
    /// a background task, but this one is the active one until further
    /// notice.
    /// </summary>
    private volatile IFileDatabaseSnapshot _currentFileDatabase;

    private long _currentFileSystemSnapshotVersion = -1;

    /// <summary>
    /// <code>true</code> if and only if 
    /// </summary>
    private bool _previousUpdateCompleted;

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
      _currentFileDatabase = _fileDatabaseSnapshotFactory.CreateEmpty();

      // Setup computing a new state everytime a new tree is computed.
      fileSystemSnapshotManager.SnapshotScanFinished += FileSystemSnapshotManagerOnSnapshotScanFinished;
      fileSystemSnapshotManager.FilesChanged += FileSystemSnapshotManagerOnFilesChanged;
    }

    public IFileDatabaseSnapshot CurrentFileDatabaseSnapshot {
      get { return _currentFileDatabase; }
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
        var matches = _currentFileDatabase.FileNames
          .AsParallel()
          // We need the line below because of "Take" (.net 4.0 PLinq
          // limitation)
          .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
          .WithCancellation(_taskCancellation.GetNewToken())
          .Where(
            item => {
              if (!searchParams.IncludeSymLinks) {
                if (_currentFileDatabase.IsContainedInSymLink(item))
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
        _currentFileDatabase.SearchableFileCount,
        Environment.ProcessorCount * 2);
      var matches = _currentFileDatabase.FileContentsPieces
        .AsParallel()
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .WithCancellation(cancellationToken)
        .Where(x => !progressTracker.ShouldEndProcessing)
        .Select(item => {
          // Filter out files inside symlinks if needed
          if (!includeSymLinks) {
            if (_currentFileDatabase.IsContainedInSymLink(item.FileName))
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
        TotalFileCount = _currentFileDatabase.SearchableFileCount,
        HitCount = progressTracker.ResultCount,
      };
    }

    private struct SearchableContentsResult {
      public FileContentsPiece FileContentsPiece { get; set; }
      public List<FilePositionSpan> Spans { get; set; }
    }

    public IEnumerable<FileExtract> GetFileExtracts(FullPath path, IEnumerable<FilePositionSpan> spans, int maxLength) {
      var filename = _fileSystemNameFactory.CreateProjectFileFromFullPath(_projectDiscovery, path);
      if (filename.IsNull)
        return Enumerable.Empty<FileExtract>();

      return _currentFileDatabase.GetFileExtracts(filename.FileName, spans, maxLength);
    }

    public event EventHandler<OperationInfo> FilesLoading;

    public event EventHandler<OperationInfo> FilesLoadingProgress;

    public event EventHandler<FilesLoadedResult> FilesLoaded;

    protected virtual void OnFilesLoading(OperationInfo e) {
      FilesLoading?.Invoke(this, e);
    }

    protected virtual void OnFilesLoadingProgress(OperationInfo e) {
      FilesLoadingProgress?.Invoke(this, e);
    }

    protected virtual void OnFilesLoaded(FilesLoadedResult e) {
      FilesLoaded?.Invoke(this, e);
    }

    private void FileSystemSnapshotManagerOnFilesChanged(object sender, FilesChangedEventArgs filesChangedEventArgs) {
      _taskQueue.Enqueue(UpdateFileContentsTaskId, cancellationToken => {
        using (new TaskQueueGuard(this)) {
          UpdateFileContentsLongTask(filesChangedEventArgs, cancellationToken);
        }
      });
    }

    private void FileSystemSnapshotManagerOnSnapshotScanFinished(object sender, SnapshotScanResult e) {
      // Skip file system scan errors and keep our current database
      if (e.Error != null)
        return;

      _taskQueue.Enqueue(ComputeNewStatedId, cancellationToken => {
        using (new TaskQueueGuard(this)) {
          ComputeNewStateLongTask(e.PreviousSnapshot, e.NewSnapshot, e.FullPathChanges, cancellationToken);
        }
      });

      // Enqueue a GC at this point makes sense as there might be a lot of
      // garbage to reclaim from previous file contents stored in native heap.
      // By performin a full GC and waiting for finalizer, we ensure that (most)
      // orphan SafeHandles are released in a timely fashion. We enqueue a
      // separate task to ensure there is no potential state keeping these
      // variables alive for slightly too long.
      _taskQueue.Enqueue(GarbageCollectId, cancellationToken => {
        Logger.LogMemoryStats();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
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
        .ToList();

      // Split patterns into "include" and "exclude" patterns.
      // "exclude" patterns are defined as any pattern starting with "-".
      var includePatterns = patterns
        .Where(x => !x.StartsWith("-"))
        .ToList();

      var excludePatterns = patterns
        .Where(x => x.StartsWith("-"))
        .Select(x => x.Substring(1))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToList();

      // Process qouble quotes, and add implicit wildcards if needed
      includePatterns = MapQuotesAndImplicitWildcards(searchParams, includePatterns).ToList();
      excludePatterns = MapQuotesAndImplicitWildcards(searchParams, excludePatterns).ToList();

      // If no include pattern, assume implicit "*"
      if (includePatterns.Count == 0) {
        includePatterns.Add("*");
      }
      var includeMatcher = new AnyPathMatcher(includePatterns.Select(PatternParser.ParsePattern));

      // Perf: Use "null" matcher if exclude pattern list is empty
      var excludeMatcher = (excludePatterns.Count == 0 ?
          null :
          new AnyPathMatcher(excludePatterns.Select(PatternParser.ParsePattern)));

      var comparer = searchParams.MatchCase ? PathComparerRegistry.CaseSensitive : PathComparerRegistry.CaseInsensitive;
      if (patterns.Any(x => x.Contains(Path.DirectorySeparatorChar))) {
        return new SearchPreProcessResult<T> {
          Matcher = (item) => {
            if (excludeMatcher != null && matchRelativeName(excludeMatcher, item, comparer))
              return false;
            return matchRelativeName(includeMatcher, item, comparer);
          }
        };
      } else {
        return new SearchPreProcessResult<T> {
          Matcher = (item) => {
            if (excludeMatcher != null && matchName(excludeMatcher, item, comparer))
              return false;
            return matchName(includeMatcher, item, comparer);
          }
        };
      }
    }

    private IEnumerable<string> MapQuotesAndImplicitWildcards(SearchParams searchParams, IEnumerable<string> patterns) {
      return patterns.Select(x => {
          // Exception to ".gitignore" syntax: If the search string doesn't contain any special
          // character, surround the pattern with "*" so that we match sub-strings.
          // TODO(rpaquay): What about "."? Special or not?
          if (x.IndexOf(Path.DirectorySeparatorChar) < 0 && x.IndexOf('*') < 0 && !IsSurroundedByDoubleQuotes(x)) {
            if (!searchParams.MatchWholeWord) {
              x = "*" + x + "*";
            }
          }
          return x;
        })
        .Select(x => RemoveSurroundingDoubleQuotes(x))
        .Where(x => !string.IsNullOrEmpty(x));
    }

    private string RemoveSurroundingDoubleQuotes(string text) {
      if (IsSurroundedByDoubleQuotes(text)) {
        return text.Substring(1, text.Length - 2);
      }
      return text;
    }

    private bool IsSurroundedByDoubleQuotes(string text) {
      return (text.Length > 2) && (text[0] == '"') && (text[text.Length - 1] == '"');
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

    private void ComputeNewStateLongTask(FileSystemSnapshot previousSnapshot, FileSystemSnapshot newSnapshot,
      FullPathChanges fullPathChanges,
      CancellationToken cancellationToken) {
      Invariants.Assert(_inTaskQueueTask);

      UpdateFileDatabase(newSnapshot, options => {
        // We only allow incremental updates if the last update was successfully completed
        // and if the file system snapshot version we are based on is the same as the
        // new file system snapshot we are processing
        if (previousSnapshot.Version == _currentFileSystemSnapshotVersion &&
            options.PreviousUpdateCompleted &&
            fullPathChanges != null) {
          return CreateWithFileSystemChanges(newSnapshot, fullPathChanges, options, cancellationToken);
        } else {
          Logger.LogInfo($"Starting a full database update: " +
                         $"CurrentSnapshotVersion={_currentFileSystemSnapshotVersion}, " +
                         $"PreviousUpdateCompleted={options.PreviousUpdateCompleted}, " +
                         $"PreviousSnapshotVersion={previousSnapshot.Version}, " +
                         $"FullPathChanges={fullPathChanges?.Entries.Count ?? -1}.");
          return CreateFullScan(newSnapshot, options, cancellationToken);
        }
      });
    }

    private void UpdateFileContentsLongTask(FilesChangedEventArgs args, CancellationToken cancellationToken) {
      Invariants.Assert(_inTaskQueueTask);

      UpdateFileDatabase(args.FileSystemSnapshot, options => {
        // We only allow incremental updates if the last update was successfully completed
        // and if the file system snapshot version we are based on is the same as the
        // new file system snapshot we are processing
        if (args.FileSystemSnapshot.Version == _currentFileSystemSnapshotVersion &&
            options.PreviousUpdateCompleted) {
          return CreateWithModifiedFiles(args.FileSystemSnapshot, args.ChangedFiles, options, cancellationToken);
        } else {
          Logger.LogInfo($"Starting a full database update: " +
                         $"CurrentSnapshotVersion={_currentFileSystemSnapshotVersion}, " +
                         $"PreviousUpdateCompleted={options.PreviousUpdateCompleted}, " +
                         $"SnapshotVersion={args.FileSystemSnapshot.Version}, " +
                         $"ChangesFiles={args.ChangedFiles?.Count ?? -1}.");
          return CreateFullScan(args.FileSystemSnapshot, options, cancellationToken);
        }
      });
    }

    private void UpdateFileDatabase(FileSystemSnapshot fileSystemSnapshot, Func<UpdateFileDatabaseOptions, IFileDatabaseSnapshot> updater) {
      Invariants.Assert(_inTaskQueueTask);

      var options = new UpdateFileDatabaseOptions();

      _operationProcessor.Execute(new OperationHandlers {
        OnBeforeExecute = info => {
          options.OperationInfo = info;
          options.PreviousUpdateCompleted = _previousUpdateCompleted;
          _previousUpdateCompleted = false;
        },

        OnError = (info, error) => {
          _currentFileSystemSnapshotVersion = fileSystemSnapshot.Version;
          _previousUpdateCompleted = false;
          OnFilesLoading(info);
          OnFilesLoaded(new FilesLoadedResult {
            OperationInfo = info,
            Error = error,
            TreeVersion = _currentFileSystemSnapshotVersion,
          });
        },

        Execute = info => {
          options.CurrentFileDatabase = _currentFileDatabase;
          var newFileDatabase = updater(options);
          ActivateCurrentDatabase(fileSystemSnapshot, newFileDatabase, true);

          OnFilesLoaded(new FilesLoadedResult {
            OperationInfo = info,
            TreeVersion = fileSystemSnapshot.Version
          });
        }
      });
    }

    private IFileDatabaseSnapshot CreateFullScan(FileSystemSnapshot newSnapshot, UpdateFileDatabaseOptions options,
      CancellationToken cancellationToken) {
      Invariants.Assert(_inTaskQueueTask);

      return _fileDatabaseSnapshotFactory.CreateIncremental(
        options.CurrentFileDatabase,
        newSnapshot,
        onIntermadiateResult: fileDatabase => {
          ActivateCurrentDatabase(newSnapshot, fileDatabase, false);
          OnFilesLoadingProgress(options.OperationInfo);
        },
        onLoading: () => OnFilesLoading(options.OperationInfo),
        onLoaded: () => OnFilesLoaded(new FilesLoadedResult {
          OperationInfo = options.OperationInfo,
          TreeVersion = _currentFileSystemSnapshotVersion,
        }),
        cancellationToken: cancellationToken);
    }

    private IFileDatabaseSnapshot CreateWithFileSystemChanges(FileSystemSnapshot newSnapshot, FullPathChanges fullPathChanges,
      UpdateFileDatabaseOptions options, CancellationToken cancellationToken) {
      Invariants.Assert(_inTaskQueueTask);

      return _fileDatabaseSnapshotFactory.CreateIncrementalWithFileSystemUpdates(
        options.CurrentFileDatabase,
        newSnapshot,
        fullPathChanges,
        onIntermadiateResult: fileDatabase => {
          ActivateCurrentDatabase(newSnapshot, fileDatabase, false);
          OnFilesLoadingProgress(options.OperationInfo);
        },
        onLoading: () => OnFilesLoading(options.OperationInfo),
        onLoaded: () => OnFilesLoaded(new FilesLoadedResult {
          OperationInfo = options.OperationInfo,
          TreeVersion = _currentFileSystemSnapshotVersion,
        }),
        cancellationToken: cancellationToken);
    }

    private IFileDatabaseSnapshot CreateWithModifiedFiles(FileSystemSnapshot fileSystemSnapshot, IList<ProjectFileName> changedFiles, UpdateFileDatabaseOptions options, CancellationToken cancellationToken) {
      Invariants.Assert(_inTaskQueueTask);

      return _fileDatabaseSnapshotFactory.CreateIncrementalWithModifiedFiles(
        options.CurrentFileDatabase,
        fileSystemSnapshot,
        changedFiles,
        onIntermadiateResult: fileDatabase => {
          ActivateCurrentDatabase(fileSystemSnapshot, fileDatabase, false);
          OnFilesLoadingProgress(options.OperationInfo);
        },
        onLoading: () => OnFilesLoading(options.OperationInfo),
        onLoaded: () => OnFilesLoaded(new FilesLoadedResult {
          OperationInfo = options.OperationInfo,
          TreeVersion = _currentFileSystemSnapshotVersion,
        }),
        cancellationToken: cancellationToken);
    }

    private void ActivateCurrentDatabase(FileSystemSnapshot fileSystemSnapshot, IFileDatabaseSnapshot databaseSnapshot, bool complete) {
      Invariants.Assert(_inTaskQueueTask);

      _currentFileDatabase = databaseSnapshot;
      _currentFileSystemSnapshotVersion = fileSystemSnapshot.Version;
      _previousUpdateCompleted = complete; // Success => we allow incremtal updates next time
    }

    private bool MatchFileName(IPathMatcher matcher, FileName fileName, IPathComparer comparer) {
      return matcher.MatchFileName(new RelativePath(fileName.Name), comparer);
    }

    private bool MatchFileRelativePath(IPathMatcher matcher, FileName fileName, IPathComparer comparer) {
      return matcher.MatchFileName(fileName.RelativePath, comparer);
    }

    private class UpdateFileDatabaseOptions {
      public OperationInfo OperationInfo { get; set; }
      public bool PreviousUpdateCompleted { get; set; }
      public IFileDatabaseSnapshot CurrentFileDatabase { get; set; }
    }

    private class TaskQueueGuard : IDisposable {
      private readonly SearchEngine _searchEngine;

      public TaskQueueGuard(SearchEngine searchEngine) {
        _searchEngine = searchEngine;
        Invariants.Assert(!_searchEngine._inTaskQueueTask);
        _searchEngine._inTaskQueueTask = true;
      }

      public void Dispose() {
        Invariants.Assert(_searchEngine._inTaskQueueTask);
        _searchEngine._inTaskQueueTask = false;
      }
    }
  }
}