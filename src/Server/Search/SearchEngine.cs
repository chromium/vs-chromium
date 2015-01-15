// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Files.PatternMatching;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
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
    private const int MinimumSearchPatternLength = 2;
    private readonly IFileDatabaseFactory _fileDatabaseFactory;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly ISearchStringParser _searchStringParser;
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
      ISearchStringParser searchStringParser,
      IOperationProcessor operationProcessor) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _taskQueue = taskQueueFactory.CreateQueue("SearchEngine Task Queue");
      _fileDatabaseFactory = fileDatabaseFactory;
      _projectDiscovery = projectDiscovery;
      _searchStringParser = searchStringParser;
      _operationProcessor = operationProcessor;

      // Create a "Null" state
      _currentFileDatabase = _fileDatabaseFactory.CreateEmpty();

      // Setup computing a new state everytime a new tree is computed.
      fileSystemProcessor.SnapshotComputed += FileSystemProcessorOnSnapshotComputed;
      fileSystemProcessor.FilesChanged += FileSystemProcessorOnFilesChanged;
    }

    public SearchFileNamesResult SearchFileNames(SearchParams searchParams) {
      var matchFunction = SearchPreProcessParams<FileName>(searchParams, MatchFileName, MatchFileRelativePath);
      if (matchFunction == null)
        return SearchFileNamesResult.Empty;

      // taskCancellation is used to make sure we cancel previous tasks as fast
      // as possible to avoid using too many CPU resources if the caller keeps
      // asking us to search for things. Note that this assumes the caller is
      // only interested in the result of the *last* query, while the previous
      // queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();
      var searchedFileCount = 0;
      var matches = _currentFileDatabase.FileNames
        .AsParallel()
        // We need the line below because of "Take" (.net 4.0 PLinq limitation)
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .WithCancellation(_taskCancellation.GetNewToken())
        .Where(item => {
          if (!searchParams.IncludeSymLinks) {
            if (_currentFileDatabase.IsContainedInSymLink(item.Parent))
              return false;
          }
          Interlocked.Increment(ref searchedFileCount);
          return matchFunction(item);
        })
        .Take(searchParams.MaxResults)
        .ToList();

      return new SearchFileNamesResult {
        FileNames = matches,
        TotalCount = searchedFileCount
      };
    }

    public SearchDirectoryNamesResult SearchDirectoryNames(SearchParams searchParams) {
      var matchFunction = SearchPreProcessParams<DirectoryName>(searchParams, MatchDirectoryName,
                                                                MatchDirectoryRelativePath);
      if (matchFunction == null)
        return SearchDirectoryNamesResult.Empty;

      // taskCancellation is used to make sure we cancel previous tasks as fast
      // as possible to avoid using too many CPU resources if the caller keeps
      // asking us to search for things. Note that this assumes the caller is
      // only interested in the result of the *last* query, while the previous
      // queries will throw an OperationCanceled exception.
      _taskCancellation.CancelAll();
      var searchedFileCount = 0;
      var matches = _currentFileDatabase.DirectoryNames
        .AsParallel()
        // We need the line below because of "Take" (.net 4.0 PLinq limitation)
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .WithCancellation(_taskCancellation.GetNewToken())
        .Where(item => {
          if (!searchParams.IncludeSymLinks) {
            if (_currentFileDatabase.IsContainedInSymLink(item))
              return false;
          }
          Interlocked.Increment(ref searchedFileCount);
          return matchFunction(item);
        })
        .Take(searchParams.MaxResults)
        .ToList();

      return new SearchDirectoryNamesResult {
        DirectoryNames = matches,
        TotalCount = searchedFileCount
      };
    }

    public SearchFileContentsResult SearchFileContents(SearchParams searchParams) {
      ParsedSearchString parsedSearchString;
      if (searchParams.Regex) {
        parsedSearchString = new ParsedSearchString(
          new ParsedSearchString.Entry { Text = searchParams.SearchString },
          Enumerable.Empty<ParsedSearchString.Entry>(),
          Enumerable.Empty<ParsedSearchString.Entry>());
      } else {
        parsedSearchString = _searchStringParser.Parse(searchParams.SearchString ?? "");
        // Don't search empty or very small strings -- no significant results.
        if (string.IsNullOrWhiteSpace(parsedSearchString.MainEntry.Text) ||
            (parsedSearchString.MainEntry.Text.Length < MinimumSearchPatternLength)) {
          return SearchFileContentsResult.Empty;
        }
      }

      var searchContentsAlgorithms = CreateSearchAlgorithms(parsedSearchString, searchParams.MatchCase, searchParams.Regex);
      using (var searchContentsData = new SearchContentsData(parsedSearchString, searchContentsAlgorithms)) {
        // taskCancellation is used to make sure we cancel previous tasks as
        // fast as possible to avoid using too many CPU resources if the caller
        // keeps asking us to search for things. Note that this assumes the
        // caller is only interested in the result of the *last* query, while
        // the previous queries will throw an OperationCanceled exception.
        _taskCancellation.CancelAll();
        var cancellationToken = _taskCancellation.GetNewToken();
        return DoSearchFileContents(searchContentsData, searchParams.MaxResults, searchParams.IncludeSymLinks, cancellationToken);
      }
    }

    private static List<SearchContentsAlgorithms> CreateSearchAlgorithms(ParsedSearchString parsedSearchString, bool matchCase, bool regex) {
      var searchOptions = NativeMethods.SearchOptions.kNone;
      if (matchCase)
        searchOptions |= NativeMethods.SearchOptions.kMatchCase;
      if (regex)
        searchOptions |= NativeMethods.SearchOptions.kRegex;
      return parsedSearchString.EntriesBeforeMainEntry
        .Concat(new[] { parsedSearchString.MainEntry })
        .Concat(parsedSearchString.EntriesAfterMainEntry)
        .OrderBy(x => x.Index)
        .Select(entry => {
          var a1 = AsciiFileContents.CreateSearchAlgo(entry.Text, searchOptions);
          var a2 = UTF16FileContents.CreateSearchAlgo(entry.Text, searchOptions);
          return new SearchContentsAlgorithms(a1, a2);
        })
        .ToList();
    }

    private SearchFileContentsResult DoSearchFileContents(SearchContentsData searchContentsData, int maxResults, bool includeSymLinks, CancellationToken cancellationToken) {
      var taskResults = new TaskResultCounter(maxResults);
      var searchedFileCount = 0;
      var matches = _currentFileDatabase.FilesWithContents
        //.AsParallel()
        //.WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        //.WithCancellation(cancellationToken)
        .Where(x => !taskResults.Done)
        .Select(item => {
          if (!includeSymLinks) {
            if (_currentFileDatabase.IsContainedInSymLink(item.FileName.Parent))
              return null;
          }
          Interlocked.Increment(ref searchedFileCount);
          return MatchFileContents(item, searchContentsData, taskResults);
        })
        .Where(r => r != null)
        .ToList();

      return new SearchFileContentsResult {
        Entries = matches,
        SearchedFileCount = searchedFileCount,
        TotalFileCount = _currentFileDatabase.FilesWithContents.Count,
        HitCount = taskResults.Count,
      };
    }

    public IEnumerable<FileExtract> GetFileExtracts(FullPath path, IEnumerable<FilePositionSpan> spans) {
      var filename = FileSystemNameFactoryExtensions.GetProjectFileName(_fileSystemNameFactory, _projectDiscovery, path);
      if (filename == null)
        return Enumerable.Empty<FileExtract>();

      return _currentFileDatabase.GetFileExtracts(filename.Item2, spans);
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

    private void UpdateFileContents(IEnumerable<Tuple<IProject, FileName>> paths) {
      _operationProcessor.Execute(new OperationHandlers {
        OnBeforeExecute = info => OnFilesLoading(info),
        OnError = (info, error) => OnFilesLoaded(new FilesLoadedResult { OperationInfo = info, Error = error }),
        Execute = info => {
          paths.ForAll(x => _currentFileDatabase.UpdateFileContents(x));
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

    private Func<T, bool> SearchPreProcessParams<T>(
      SearchParams searchParams,
      Func<IPathMatcher, T, IPathComparer, bool> matchName,
      Func<IPathMatcher, T, IPathComparer, bool> matchRelativeName) where T : FileSystemName {
      var pattern = ConvertUserSearchStringToSearchPattern(searchParams);
      if (pattern == null)
        return null;

      var matcher = FileNameMatching.ParsePattern(pattern);

      var comparer = searchParams.MatchCase ?
                       PathComparerRegistry.CaseSensitive :
                       PathComparerRegistry.CaseInsensitive;
      if (pattern.Contains(Path.DirectorySeparatorChar))
        return (item) => matchRelativeName(matcher, item, comparer);
      else
        return (item) => matchName(matcher, item, comparer);
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
        pattern = "*" + pattern + "*";
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

    private static FileSearchResult MatchFileContents(FileData fileData, SearchContentsData searchContentsData, TaskResultCounter taskResultCounter) {
      var spans = fileData.Contents.Search(searchContentsData);
      if (spans.Count == 0)
        return null;

      taskResultCounter.Add(spans.Count);

      return new FileSearchResult {
        FileName = fileData.FileName,
        Spans = spans
      };
    }
  }
}
