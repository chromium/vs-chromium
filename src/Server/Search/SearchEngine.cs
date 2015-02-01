// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Files.PatternMatching;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Operations;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.Search {
  [Export(typeof(ISearchEngine))]
  public class SearchEngine : ISearchEngine {
    private static readonly TaskId ComputeNewStatedId = new TaskId("ComputeNewStateId");
    private static readonly TaskId GarbageCollectId = new TaskId("GarbageCollectId");
    private readonly IFileDatabaseFactory _fileDatabaseFactory;
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
    private volatile IFileDatabase _currentFileDatabase;

    [ImportingConstructor]
    public SearchEngine(
      IFileSystemProcessor fileSystemProcessor,
      IFileSystemNameFactory fileSystemNameFactory,
      ITaskQueueFactory taskQueueFactory,
      IFileDatabaseFactory fileDatabaseFactory,
      IProjectDiscovery projectDiscovery,
      ICompiledTextSearchDataFactory compiledTextSearchDataFactory,
      IOperationProcessor operationProcessor) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _taskQueue = taskQueueFactory.CreateQueue("SearchEngine Task Queue");
      _fileDatabaseFactory = fileDatabaseFactory;
      _projectDiscovery = projectDiscovery;
      _compiledTextSearchDataFactory = compiledTextSearchDataFactory;
      _operationProcessor = operationProcessor;

      // Create a "Null" state
      _currentFileDatabase = _fileDatabaseFactory.CreateEmpty();

      // Setup computing a new state everytime a new tree is computed.
      fileSystemProcessor.SnapshotComputed += FileSystemProcessorOnSnapshotComputed;
      fileSystemProcessor.FilesChanged += FileSystemProcessorOnFilesChanged;
    }

    public SearchFileNamesResult SearchFileNames(SearchParams searchParams) {
      // taskCancellation is used to make sure we cancel previous tasks as fast
      // as possible to avoid using too many CPU resources if the caller keeps
      // asking us to search for things. Note that this assumes the caller is
      // only interested in the result of the *last* query, while the previous
      // queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();

      var preProcessResult = PreProcessSearch<FileName>(
        searchParams,
        MatchFileName,
        MatchFileRelativePath);
      if (preProcessResult == null)
        return SearchFileNamesResult.Empty;

      using (preProcessResult) {
        var searchedFileCount = 0;
        var matches = _currentFileDatabase.FileNames
          .AsParallel()
          // We need the line below because of "Take" (.net 4.0 PLinq limitation)
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

        return new SearchFileNamesResult {
          FileNames = matches,
          TotalCount = searchedFileCount
        };
      }
    }

    public SearchDirectoryNamesResult SearchDirectoryNames(SearchParams searchParams) {
      // taskCancellation is used to make sure we cancel previous tasks as fast
      // as possible to avoid using too many CPU resources if the caller keeps
      // asking us to search for things. Note that this assumes the caller is
      // only interested in the result of the *last* query, while the previous
      // queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();

      var preProcessResult = PreProcessSearch<DirectoryName>(
        searchParams,
        MatchDirectoryName,
        MatchDirectoryRelativePath);
      if (preProcessResult == null)
        return SearchDirectoryNamesResult.Empty;

      using (preProcessResult) {
        var searchedFileCount = 0;
        var matches = _currentFileDatabase.DirectoryNames
          .AsParallel()
          // We need the line below because of "Take" (.net 4.0 PLinq limitation)
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

        return new SearchDirectoryNamesResult {
          DirectoryNames = matches,
          TotalCount = searchedFileCount
        };
      }
    }

    public SearchTextResult SearchText(SearchParams searchParams) {
      // taskCancellation is used to make sure we cancel previous tasks as
      // fast as possible to avoid using too many CPU resources if the caller
      // keeps asking us to search for things. Note that this assumes the
      // caller is only interested in the result of the *last* query, while
      // the previous queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();

      using (var searchContentsData = _compiledTextSearchDataFactory.Create(searchParams)) {
        var cancellationToken = _taskCancellation.GetNewToken();
        return SearchTextWorker(
          searchContentsData,
          searchParams.MaxResults,
          searchParams.IncludeSymLinks,
          cancellationToken);
      }
    }

    private SearchTextResult SearchTextWorker(
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
          if (!includeSymLinks) {
            if (_currentFileDatabase.IsContainedInSymLink(item.FileName))
              return new SearchableContentsResult();
          }
          searchedFileIds.Set(item.FileId, true);
          return new SearchableContentsResult {
            FileContentsPiece = item,
            Spans = item
              .FindAll(compiledTextSearchData, progressTracker)
              .Select(x => new FilePositionSpan {
                Position = (int)x.CharacterOffset,
                Length = (int)x.CharacterCount,
              }).
              ToList(),
          };
        })
        .Where(r => r.Spans != null && r.Spans.Count > 0)
        .GroupBy(r => r.FileContentsPiece.FileId)
        .Select(g => new FileSearchResult {
          FileName = g.First().FileContentsPiece.FileName,
          Spans = g.OrderBy(x => x.Spans.First().Position).SelectMany(x => x.Spans).ToList()
        })
        .ToList();

      return new SearchTextResult {
        Entries = matches,
        SearchedFileCount = searchedFileIds.Count,
        TotalFileCount = _currentFileDatabase.SearchableFileCount,
        HitCount = progressTracker.ResultCount,
      };
    }

    private struct SearchableContentsResult {
      public IFileContentsPiece FileContentsPiece { get; set; }
      public List<FilePositionSpan> Spans { get; set; }
    }

    public IEnumerable<FileExtract> GetFileExtracts(FullPath path, IEnumerable<FilePositionSpan> spans, int maxLength) {
      var filename = FileSystemNameFactoryExtensions.GetProjectFileName(_fileSystemNameFactory, _projectDiscovery, path);
      if (filename == null)
        return Enumerable.Empty<FileExtract>();

      return _currentFileDatabase.GetFileExtracts(filename.Item2, spans, maxLength);
    }

    public event EventHandler<OperationInfo> FilesLoading;

    public event EventHandler<FilesLoadedResult> FilesLoaded;

    protected virtual void OnFilesLoaded(FilesLoadedResult e) {
      EventHandler<FilesLoadedResult> handler = FilesLoaded;
      if (handler != null) handler(this, e);
    }

    protected virtual void OnFilesLoading(OperationInfo e) {
      EventHandler<OperationInfo> handler = FilesLoading;
      if (handler != null) handler(this, e);
    }

    private void FileSystemProcessorOnFilesChanged(object sender, FilesChangedEventArgs filesChangedEventArgs) {
      _taskQueue.Enqueue(new TaskId("FileSystemProcessorOnFilesChanged"), () => UpdateFileContents(filesChangedEventArgs.ChangedFiles));
    }

    private void UpdateFileContents(IEnumerable<Tuple<IProject, FileName>> files) {
      _operationProcessor.Execute(new OperationHandlers {
        OnBeforeExecute = info => OnFilesLoading(info),
        OnError = (info, error) => OnFilesLoaded(new FilesLoadedResult { OperationInfo = info, Error = error }),
        Execute = info => {
          _currentFileDatabase = _fileDatabaseFactory.CreateWithChangedFiles(_currentFileDatabase, files);
          OnFilesLoaded(new FilesLoadedResult { OperationInfo = info });
        }
      });
    }

    private void FileSystemProcessorOnSnapshotComputed(object sender, SnapshotComputedResult e) {
      if (e.Error != null)
        return;

      _taskQueue.Enqueue(ComputeNewStatedId, () => ComputeNewState(e.NewSnapshot));

      // Enqueue a GC at this point makes sense as there might be a lot of
      // garbage to reclaim from previous file contents stored in native heap.
      // By performin a full GC and waiting for finalizer, we ensure that (most)
      // orphan SafeHandles are released in a timely fashion. We enqueue a
      // separate task to ensure there is no potential state keeping these
      // variables alive for slightly too long.
      _taskQueue.Enqueue(GarbageCollectId, () => {
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

    private SearchPreProcessResult<T> PreProcessSearch<T>(
      SearchParams searchParams,
      Func<IPathMatcher, T, IPathComparer, bool> matchName,
      Func<IPathMatcher, T, IPathComparer, bool> matchRelativeName) where T : FileSystemName {
      var pattern = ConvertUserSearchStringToSearchPattern(searchParams);
      if (pattern == null)
        return null;

      if (searchParams.Regex) {
        return PreProcessRegularExpressionSearch(searchParams, matchName, matchRelativeName);
      }
      var matcher = FileNameMatching.ParsePattern(pattern);

      var comparer = searchParams.MatchCase ?
                       PathComparerRegistry.CaseSensitive :
                       PathComparerRegistry.CaseInsensitive;
      if (pattern.Contains(Path.DirectorySeparatorChar)) {
        return new SearchPreProcessResult<T> {
          Matcher = (item) => matchRelativeName(matcher, item, comparer)
        };
      } else {
        return new SearchPreProcessResult<T> {
          Matcher = (item) => matchName(matcher, item, comparer)
        };
      }
    }

    private SearchPreProcessResult<T> PreProcessRegularExpressionSearch<T>(
      SearchParams searchParams,
      Func<IPathMatcher, T, IPathComparer, bool> matchName,
      Func<IPathMatcher, T, IPathComparer, bool> matchRelativeName) where T : FileSystemName {

      var pattern = searchParams.SearchString ?? "";
      pattern = pattern.Trim();
      if (string.IsNullOrWhiteSpace(pattern))
        return null;

      var data = _compiledTextSearchDataFactory.Create(searchParams);
      var provider = data.GetSearchAlgorithmProvider(data.ParsedSearchString.MainEntry);
      var matcher = new CompiledTextSearchProviderPathMatcher(provider);
      var comparer = searchParams.MatchCase ?
                       PathComparerRegistry.CaseSensitive :
                       PathComparerRegistry.CaseInsensitive;
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
      private readonly ICompiledTextSearchProvider _searchProvider;

      public CompiledTextSearchProviderPathMatcher(ICompiledTextSearchProvider searchProvider) {
        _searchProvider = searchProvider;
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

          var hit = _searchProvider.GetAsciiSearch()
            .FindFirst(range, OperationProgressTracker.None);
          if (hit.HasValue)
            return true;

          if (!hasDirectorySeparators)
            return false;

          // Replace '\' with '/' and try again
          var first = (byte *)ptr.ToPointer();
          var last = first + value.Length;
          for (; first != last; first++) {
            if ((char)(*first) == Path.DirectorySeparatorChar) {
              *first = (byte)Path.AltDirectorySeparatorChar;
            }
          }

          // Search again
          hit = _searchProvider.GetAsciiSearch()
                      .FindFirst(range, OperationProgressTracker.None);
          return hit.HasValue;
        }
        finally {
          Marshal.FreeHGlobal(ptr);
        }
      }
    }

    private static string ConvertUserSearchStringToSearchPattern(SearchParams searchParams) {
      var pattern = searchParams.SearchString ?? "";

      pattern = pattern.Trim();
      if (string.IsNullOrWhiteSpace(pattern))
        return null;

      // We use "\\" internally for paths and patterns.
      pattern = pattern.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

      // Exception to ".gitignore" syntax: If the search string doesn't contain any special
      // character, surround the pattern with "*" so that we match sub-strings.
      // TODO(rpaquay): What about "."? Special or not?
      if (pattern.IndexOf(Path.DirectorySeparatorChar) < 0 &&
          pattern.IndexOf('*') < 0) {
        if (!searchParams.MatchWholeWord) {
          pattern = "*" + pattern + "*";
        }
      }

      return pattern;
    }

    private void ComputeNewState(FileSystemTreeSnapshot newSnapshot) {
      _operationProcessor.Execute(new OperationHandlers {
        OnBeforeExecute = info => OnFilesLoading(info),
        OnError = (info, error) => OnFilesLoaded(new FilesLoadedResult { OperationInfo = info, Error = error }),
        Execute = info => {
          Logger.Log("Computing new state of file database from file system tree.");
          var sw = Stopwatch.StartNew();

          var oldState = _currentFileDatabase;
          var newState = _fileDatabaseFactory.CreateIncremental(oldState, newSnapshot);

          sw.Stop();
          Logger.Log(">>>>>>>> Done computing new state of file database from file system tree in {0:n0} msec.",
            sw.ElapsedMilliseconds);
          Logger.LogMemoryStats();

          // Store and activate new state (atomic operation).
          _currentFileDatabase = newState;
          OnFilesLoaded(new FilesLoadedResult { OperationInfo = info });
        }
      });
    }

    private bool MatchFileName(IPathMatcher matcher, FileName fileName, IPathComparer comparer) {
      return matcher.MatchFileName(new RelativePath(fileName.RelativePath.FileName), comparer);
    }

    private bool MatchFileRelativePath(IPathMatcher matcher, FileName fileName, IPathComparer comparer) {
      return matcher.MatchFileName(fileName.RelativePath, comparer);
    }

    private bool MatchDirectoryName(IPathMatcher matcher, DirectoryName directoryName, IPathComparer comparer) {
      // "Chromium" root directories make it through here, skip them.
      if (directoryName.IsAbsoluteName)
        return false;

      return matcher.MatchDirectoryName(new RelativePath(directoryName.RelativePath.FileName), comparer);
    }

    private bool MatchDirectoryRelativePath(IPathMatcher matcher, DirectoryName directoryName, IPathComparer comparer) {
      // "Chromium" root directories make it through here, skip them.
      if (directoryName.IsAbsoluteName)
        return false;

      return matcher.MatchDirectoryName(directoryName.RelativePath, comparer);
    }
  }
}
