// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Files.PatternMatching;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;
using VsChromium.Core.Utility;
using VsChromium.Server.Diagnostics;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.TextSearch;
using VsChromium.Server.Operations;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.Search {
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

    public SearchFilePathsResult SearchFilePaths(SearchParams searchParams, CancellationToken cancellationToken = default) {
      cancellationToken.ThrowIfCancellationRequested();

      // taskCancellation is used to make sure we cancel previous tasks as fast
      // as possible to avoid using too many CPU resources if the caller keeps
      // asking us to search for things. Note that this assumes the caller is
      // only interested in the result of the *last* query, while the previous
      // queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();

      using (var cancellationScope = _taskCancellation.CreateScope(cancellationToken)) {
        var linkedToken = cancellationScope.Token;
        var preProcessResult = PreProcessFileSystemNameSearch<FileName>(
          searchParams,
          MatchFileName,
          MatchFileRelativePath);
        if (preProcessResult == null) {
          linkedToken.ThrowIfCancellationRequested();
          return SearchFilePathsResult.Empty;
        }

        using (preProcessResult) {
          // Note: Partitions of the file list are processed in parallel, each with its own local
          // counter and list of matches (no shared state per file), and the search stops as soon as
          // "MaxResults" matches have been found.
          var database = _currentFileDatabase;
          var fileNames = database.FileNames;
          var matcher = preProcessResult.Matcher;
          var includeSymLinks = searchParams.IncludeSymLinks;
          var maxResults = searchParams.MaxResults;
          var matches = new List<FileName>();
          var searchedFileCount = 0;
          var gate = new object();
          var batchSize = Math.Max(256, fileNames.Count / (Environment.ProcessorCount * 16));
          ParallelBatches.For(fileNames.Count, batchSize, linkedToken, (start, end) => {
            List<FileName> localMatches = null;
            var localSearchedFileCount = 0;
            for (var i = start; i < end; i++) {
              if (linkedToken.IsCancellationRequested)
                break;
              var item = fileNames[i];
              if (!includeSymLinks && database.IsContainedInSymLink(item))
                continue;
              localSearchedFileCount++;
              if (matcher(item)) {
                (localMatches ??= new List<FileName>()).Add(item);
              }
            }

            if (linkedToken.IsCancellationRequested)
              return false;

            lock (gate) {
              searchedFileCount += localSearchedFileCount;
              if (localMatches != null) {
                matches.AddRange(localMatches);
              }
              // Stop when enough matches have been found
              return matches.Count < maxResults;
            }
          });

          linkedToken.ThrowIfCancellationRequested();

          if (matches.Count > maxResults) {
            matches.RemoveRange(Math.Max(0, maxResults), matches.Count - Math.Max(0, maxResults));
          }

          return new SearchFilePathsResult {
            FileNames = matches,
            TotalCount = searchedFileCount
          };
        }
      }
    }

    public SearchCodeResult SearchCode(SearchParams searchParams, CancellationToken cancellationToken = default) {
      cancellationToken.ThrowIfCancellationRequested();

      // taskCancellation is used to make sure we cancel previous tasks as fast
      // as possible to avoid using too many CPU resources if the caller keeps
      // asking us to search for things. Note that this assumes the caller is
      // only interested in the result of the *last* query, while the previous
      // queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();

      using (var cancellationScope = _taskCancellation.CreateScope(cancellationToken)) {
        var linkedToken = cancellationScope.Token;

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
          return SearchCodeWorker(
            searchContentsData,
            searchParams.MaxResults,
            searchParams.IncludeSymLinks,
            linkedToken);
        }
      }
    }

    private SearchCodeResult SearchCodeWorker(
      CompiledTextSearchData compiledTextSearchData,
      int maxResults,
      bool includeSymLinks,
      CancellationToken cancellationToken) {
      // Note: The pieces are processed in parallel by batches (dynamic load balancing), each worker
      // with its own list of results and counter of searched files: no allocation for pieces
      // without match, no shared state per piece. The snapshot is read once, so that all the
      // values below are consistent even if a new snapshot is activated during the search.
      var database = _currentFileDatabase;
      var pieces = database.FileContentsPieces;
      var progressTracker = new OperationProgressTracker(maxResults, cancellationToken);
      var fileNameFilter = compiledTextSearchData.FileNameFilter;
      var trigramQuery = compiledTextSearchData.TrigramQuery;
      var results = new List<SearchableContentsResult>();
      long searchedFileCount = 0;
      var gate = new object();
      var batchSize = Math.Max(16, pieces.Count / (Environment.ProcessorCount * 16));
      ParallelBatches.For(pieces.Count, batchSize, cancellationToken, (start, end) => {
        List<SearchableContentsResult> localResults = null;
        long localSearchedFileCount = 0;
        for (var i = start; i < end; i++) {
          if (progressTracker.ShouldEndProcessing)
            break;

          var item = pieces[i];
          // Filter out files inside symlinks if needed
          if (!includeSymLinks && database.IsContainedInSymLink(item.FileName))
            continue;
          // Filter out files that don't match the file name match pattern
          if (!fileNameFilter(item.FileName))
            continue;
          // Count each file once (its first piece)
          if (item.IsFirstPiece)
            localSearchedFileCount++;
          // Skip pieces that can't contain a match (trigram filters)
          if (trigramQuery != null && !item.MayContain(trigramQuery))
            continue;

          var ranges = item.FindAll(compiledTextSearchData, progressTracker);
          if (ranges.Count == 0)
            continue;

          var spans = new List<FilePositionSpan>(ranges.Count);
          for (var j = 0; j < ranges.Count; j++) {
            spans.Add(new FilePositionSpan { Position = ranges[j].Position, Length = ranges[j].Length });
          }
          (localResults ??= new List<SearchableContentsResult>()).Add(new SearchableContentsResult {
            FileContentsPiece = item,
            Spans = spans,
          });
        }

        lock (gate) {
          searchedFileCount += localSearchedFileCount;
          if (localResults != null)
            results.AddRange(localResults);
        }
        return !progressTracker.ShouldEndProcessing;
      });

      cancellationToken.ThrowIfCancellationRequested();

      var matches = results
        .GroupBy(r => r.FileContentsPiece.FileId)
        .Select(g => new FileSearchResult {
          FileName = g.First().FileContentsPiece.FileName,
          Spans = g.OrderBy(x => x.Spans[0].Position).SelectMany(x => x.Spans).ToList()
        })
        .ToList();

      return new SearchCodeResult {
        Entries = matches,
        SearchedFileCount = searchedFileCount,
        TotalFileCount = database.SearchableFileCount,
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
        // Note: A background (non blocking) collection, so that searches running concurrently are
        // not paused for the duration of a full blocking collection. The finalizers of the
        // released file contents (native memory) run after it, on the finalizer thread.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false);
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
      var provider = data.GetSearchContainer(data.ParsedSearchString.LongestEntry);
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

    public class CompiledTextSearchProviderPathMatcher : IPathMatcher {
      private readonly ICompiledTextSearchContainer _searchContainer;

      public CompiledTextSearchProviderPathMatcher(ICompiledTextSearchContainer searchContainer) {
        _searchContainer = searchContainer;
      }

      public bool MatchDirectoryName(ReadOnlySpan<char> path, IPathComparer comparer) {
        return MatchRelativePath(path);
      }

      public bool MatchFileName(ReadOnlySpan<char> path, IPathComparer comparer) {
        return MatchRelativePath(path);
      }

      private bool MatchRelativePath(ReadOnlySpan<char> path) {
        if (Match(path))
          return true;

        if (Path.DirectorySeparatorChar == Path.AltDirectorySeparatorChar ||
            path.IndexOf(Path.DirectorySeparatorChar) < 0)
          return false;

        // Replace '\' with '/' and try again
        char[] rented = null;
        Span<char> copy = path.Length <= 512
          ? stackalloc char[path.Length]
          : (rented = System.Buffers.ArrayPool<char>.Shared.Rent(path.Length)).AsSpan(0, path.Length);
        try {
          path.Replace(copy, Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
          return Match(copy);
        }
        finally {
          if (rented != null)
            System.Buffers.ArrayPool<char>.Shared.Return(rented);
        }
      }

      private unsafe bool Match(ReadOnlySpan<char> value) {
        fixed (char* ptr = value) {
          var fragment = new TextFragment(new IntPtr(ptr), 0, value.Length, sizeof(char));
          var hit = _searchContainer.GetUtf16Search()
            .FindFirst(fragment, OperationProgressTracker.None);
          return hit.HasValue;
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

      using (OperationTimings.Measure(OperationTimings.FileDatabaseUpdate))
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
        // Note: "FilesLoaded" is raised by UpdateFileDatabase, after the new database is
        // activated: clients search as soon as they are notified.
        onLoaded: () => { },
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
        // Note: "FilesLoaded" is raised by UpdateFileDatabase, after the new database is
        // activated: clients search as soon as they are notified.
        onLoaded: () => { },
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
        // Note: "FilesLoaded" is raised by UpdateFileDatabase, after the new database is
        // activated: clients search as soon as they are notified.
        onLoaded: () => { },
        cancellationToken: cancellationToken);
    }

    private void ActivateCurrentDatabase(FileSystemSnapshot fileSystemSnapshot, IFileDatabaseSnapshot databaseSnapshot, bool complete) {
      Invariants.Assert(_inTaskQueueTask);

      // Note: Done here (indexing thread) rather than lazily by the first search.
      databaseSnapshot.PrecomputeSearchData();
      _currentFileDatabase = databaseSnapshot;
      _currentFileSystemSnapshotVersion = fileSystemSnapshot.Version;
      _previousUpdateCompleted = complete; // Success => we allow incremtal updates next time
      _currentFileDatabaseComplete = complete;
      if (complete) {
        StartUpdatingTrigramFilters(databaseSnapshot);
      }
    }

    /// <summary>
    /// True if <see cref="_currentFileDatabase"/> is the result of a complete update (not an
    /// intermediate result of a load in progress).
    /// </summary>
    private volatile bool _currentFileDatabaseComplete;

    /// <summary>
    /// The trigram filter setting of the projects (see <see cref="SetProjectOptions"/>), by project
    /// root path. Projects not in this dictionary have their filters enabled.
    /// </summary>
    private readonly ConcurrentDictionary<FullPath, bool> _trigramFiltersEnabled = new ConcurrentDictionary<FullPath, bool>();

    private readonly object _trigramFiltersLock = new object();
    private CancellationTokenSource _trigramFiltersCancellation;
    private System.Threading.Tasks.Task _trigramFiltersTask = System.Threading.Tasks.Task.CompletedTask;

    public void SetProjectOptions(FullPath path, bool? trigramFiltersEnabled) {
      // Note: The path may be any path inside the project (or a project not known yet).
      var projectRoot = _projectDiscovery.GetProjectFromAnyPath(path)?.RootPath ?? path;
      if (trigramFiltersEnabled.HasValue) {
        var previous = IsTrigramFiltersEnabled(projectRoot);
        _trigramFiltersEnabled[projectRoot] = trigramFiltersEnabled.Value;
        Logger.LogInfo("Trigram filters of project \"{0}\": {1}", projectRoot, trigramFiltersEnabled.Value ? "enabled" : "disabled");
        // Apply to the current database, unless a load is in progress (the setting is applied
        // when the load completes).
        if (previous != trigramFiltersEnabled.Value && _currentFileDatabaseComplete) {
          StartUpdatingTrigramFilters(_currentFileDatabase);
        }
      }
    }

    public bool IsTrigramFiltersEnabled(FullPath projectRootPath) {
      return TrigramFilters.Enabled &&
             (!_trigramFiltersEnabled.TryGetValue(projectRootPath, out var enabled) || enabled);
    }

    public long GetTrigramFiltersBytes(FullPath projectRootPath) {
      long total = 0;
      var roots = new Dictionary<DirectoryName, bool>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
      foreach (var piece in _currentFileDatabase.FileContentsPieces) {
        if (!piece.IsFirstPiece)
          continue;
        var root = GetProjectRootDirectory(piece.FileName);
        if (!roots.TryGetValue(root, out var inProject)) {
          inProject = root.FullPath.Equals(projectRootPath);
          roots.Add(root, inProject);
        }
        if (inProject)
          total += piece.FileContents.TrigramFiltersByteSize;
      }
      return total;
    }

    private static DirectoryName GetProjectRootDirectory(FileName fileName) {
      var directory = fileName.Parent;
      while (directory.Parent != null)
        directory = directory.Parent;
      return directory;
    }

    /// <summary>
    /// Builds the trigram filters of the file contents of <paramref name="database"/> that don't
    /// have them yet (all of them after a full load, only the new ones after an incremental
    /// update), and releases the filters of the projects where they are disabled. Runs in the
    /// background on half of the processors: the index is usable before (searches scan the pieces
    /// without filters), so that the filters don't delay the end of the loading. Runs are
    /// serialized: a new run cancels the current one and starts after it.
    /// </summary>
    private void StartUpdatingTrigramFilters(IFileDatabaseSnapshot database) {
      if (!TrigramFilters.Enabled)
        return;
      lock (_trigramFiltersLock) {
        _trigramFiltersCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _trigramFiltersCancellation = cancellation;
        _trigramFiltersTask = _trigramFiltersTask.ContinueWith(
          _ => UpdateTrigramFilters(database, cancellation.Token),
          System.Threading.Tasks.TaskScheduler.Default);
      }
    }

    private void UpdateTrigramFilters(IFileDatabaseSnapshot database, CancellationToken cancellationToken) {
      var pieces = database.FileContentsPieces;
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      // Project setting of each project root directory (few entries).
      var enabledByRoot = new ConcurrentDictionary<DirectoryName, bool>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
      long droppedBytes = 0;
      try {
        System.Threading.Tasks.Parallel.For(0, pieces.Count, new System.Threading.Tasks.ParallelOptions {
          CancellationToken = cancellationToken,
          MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
        }, i => {
          var piece = pieces[i];
          if (!piece.IsFirstPiece)
            return;
          var root = GetProjectRootDirectory(piece.FileName);
          var enabled = enabledByRoot.GetOrAdd(root, r => IsTrigramFiltersEnabled(r.FullPath));
          if (enabled) {
            piece.FileContents.BuildTrigramFilters();
          } else {
            var bytes = piece.FileContents.DropTrigramFilters();
            if (bytes > 0)
              Interlocked.Add(ref droppedBytes, bytes);
          }
        });
        OperationTimings.Record(OperationTimings.TrigramFiltersBuild, stopwatch.Elapsed);
      }
      catch (OperationCanceledException) {
        // A newer snapshot has been activated, or a project setting changed
      }
      catch (Exception e) {
        Logger.LogError(e, "Error building trigram filters");
      }

      if (droppedBytes > 0) {
        Logger.LogInfo("Released {0:n0} bytes of trigram filters", droppedBytes);
        // Return the memory of the released filters (managed arrays) without waiting for the next
        // full collection (non blocking: searches are not paused).
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false, compacting: true);
      }
    }

    private bool MatchFileName(IPathMatcher matcher, FileName fileName, IPathComparer comparer) {
      return matcher.MatchFileName(fileName.Name.AsSpan(), comparer);
    }

    private bool MatchFileRelativePath(IPathMatcher matcher, FileName fileName, IPathComparer comparer) {
      // Note: The relative path is built in a temporary buffer (no allocation per file).
      return fileName.TestRelativePath(static (path, state) => state.matcher.MatchFileName(path, state.comparer), (matcher, comparer));
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
