// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Memory;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Cache {
  /// <summary>
  /// A file to write to the cache: its name, its metadata from the scan, and its contents in the
  /// index (null if it is not searchable).
  /// </summary>
  public readonly struct IndexCacheWriteItem {
    public IndexCacheWriteItem(string name, FileMetadata metadata, FileContents contents) {
      Name = name;
      Metadata = metadata;
      Contents = contents;
    }

    public string Name { get; }
    public FileMetadata Metadata { get; }
    public FileContents Contents { get; }
  }

  /// <summary>
  /// A directory of the tree to write to the cache: its path relative to the project root ("" for
  /// the root) and its files. The directories are written in depth first order (parents first).
  /// </summary>
  public sealed class IndexCacheWriteDirectory {
    public IndexCacheWriteDirectory(string path, bool isSymLink, IReadOnlyList<IndexCacheWriteItem> files) {
      Path = path;
      IsSymLink = isSymLink;
      Files = files;
    }

    public string Path { get; }
    public bool IsSymLink { get; }
    public IReadOnlyList<IndexCacheWriteItem> Files { get; }
  }

  /// <summary>
  /// The result of a write of the cache of a project (statistics).
  /// </summary>
  public sealed class IndexCacheWriteResult {
    /// <summary>Files with cached contents (text or binary).</summary>
    public int EntryCount;
    /// <summary>Files and directories of the tree.</summary>
    public int TreeFileCount;
    public int TreeDirectoryCount;
    /// <summary>Files whose contents were written to new arena files.</summary>
    public int NewFileCount;
    /// <summary>Files modified less than 2 seconds before they were read (see <see cref="FileContents.IsRacy"/>).</summary>
    public int RacyFileCount;
    /// <summary>
    /// Files whose contents were read with another time or size than the scan (saved with the
    /// metadata of the read, if known).
    /// </summary>
    public int ChangedFileCount;
    /// <summary>A few of the <see cref="ChangedFileCount"/> files, described (diagnostics).</summary>
    public List<string> ChangedFileSamples = new List<string>();
    public int LargeFileCount;
    public int NewArenaCount;
    public long BytesWritten;
    public int DeletedArenaCount;
    /// <summary>Arena files compacted (their live slots copied to new arena files).</summary>
    public int CompactedArenaCount;
    /// <summary>Bytes of the live slots copied by the compaction.</summary>
    public long CompactedBytes;
    public TimeSpan Elapsed;
  }

  /// <summary>
  /// The arena files of the index of a cache: bytes used (from the arena table) and bytes of the
  /// slots referenced by the entries (live). The rest is dead: slots of files modified or deleted
  /// since they were written.
  /// </summary>
  public sealed class IndexCacheArenaUsage {
    public static readonly IndexCacheArenaUsage Empty = new IndexCacheArenaUsage(new Dictionary<int, (int, long)>());

    public IndexCacheArenaUsage(IReadOnlyDictionary<int, (int Used, long Live)> arenas) {
      Arenas = arenas;
      foreach (var (used, live) in arenas.Values) {
        UsedBytes += used;
        LiveBytes += live;
      }
    }

    /// <summary>Arena id => bytes used, bytes of the live slots.</summary>
    public IReadOnlyDictionary<int, (int Used, long Live)> Arenas { get; }
    public int ArenaCount => Arenas.Count;
    public long UsedBytes { get; }
    public long LiveBytes { get; }
    public long DeadBytes => UsedBytes - LiveBytes;
  }

  /// <summary>
  /// The cache of the file contents of a project (one directory per project root, see
  /// <see cref="IndexCacheFile"/> for the format), used to load the contents of files without
  /// reading them: the contents of a file are valid if its path, last write time and size are the
  /// ones of the scan (rule of the git index).
  /// <para>
  /// The arena files are memory mapped (read-only): the contents of files loaded from the cache
  /// are <see cref="Utf8FileContents"/> over the mapped views (not native heap memory). A view stays
  /// mapped as long as file contents reference it.
  /// </para>
  /// <para>
  /// A single server writes the cache of a project: the one holding the <c>lock</c> file (opened
  /// exclusively for the lifetime of the server). The others use the cache read-only.
  /// </para>
  /// <para>
  /// The cache is never a source of truth: any error reading it (missing, truncated or corrupt
  /// files, other format version) is logged, the cache is deleted (by the writing server) and the
  /// files are read from disk.
  /// </para>
  /// </summary>
  public sealed class ProjectIndexCache : IDisposable {
    /// <summary>Files larger than this are never cached (decision: few files, fast to read).</summary>
    public const int MaxCachedFileLength = ArenaAllocator.DefaultLargeAllocationSize;

    /// <summary>
    /// The contents of files modified within this interval before they were read are not written
    /// to the cache ("racy clean" problem of git): a file modified again within the granularity of
    /// its last write time could have new contents with the same time and size (see <see
    /// cref="FileContents.IsRacy"/>).
    /// </summary>
    public static readonly TimeSpan RacyInterval = FileContentsFactory.RacyInterval;

    private const string LockFileName = "lock";
    private const string RootFileName = "root.txt";
    private const string TempIndexFileName = IndexCacheFile.IndexFileName + ".tmp";

    private readonly object _writeLock = new object();
    private readonly string _rootPath;
    private readonly bool _caseSensitive;
    private readonly string _arenasDirectory;
    private readonly string _indexPath;
    private FileStream _lockFile;
    private volatile IndexCacheLookup _lookup;
    private CancellationTokenSource _preReadCancellation;

    // Writer state (under _writeLock)
    /// <summary>
    /// The location in the arena files of the contents in memory (by memory block): mapped views
    /// of arena files, and native arenas whose contents were written (so that they are not written
    /// again). Weak keys: the entries are released with their block.
    /// </summary>
    private volatile ConditionalWeakTable<SafeHandle, BlockLocations> _locations = new ConditionalWeakTable<SafeHandle, BlockLocations>();
    /// <summary>The arena files known to be complete: id => bytes used.</summary>
    private readonly Dictionary<int, int> _validArenas = new Dictionary<int, int>();
    private int _nextArenaId = 1;
    private volatile IndexCacheArenaUsage _arenaUsage = IndexCacheArenaUsage.Empty;
    /// <summary>
    /// The compaction of the last write stopped at <see cref="CompactionMaxBytes"/> with more than
    /// <see cref="CompactionTargetDeadFraction"/> of dead bytes: the next write continues it.
    /// </summary>
    private bool _compactionPending;

    /// <summary>
    /// The arena files of the index (written or loaded): used, live and dead bytes. Computed when
    /// the index is read or written (no walk at request time).
    /// </summary>
    public IndexCacheArenaUsage ArenaUsage => _arenaUsage;

    private long _sizeOnDiskBytes;
    private long _lastSavedTicks;

    /// <summary>
    /// The size of the index and of its arena files, from the last read or write of the index (no
    /// disk access): 0 if there is no index.
    /// </summary>
    public long SizeOnDiskBytes => Interlocked.Read(ref _sizeOnDiskBytes);

    /// <summary>The last write time of the index (when it was read or written), if any.</summary>
    public DateTime? LastSavedUtc {
      get {
        var ticks = Interlocked.Read(ref _lastSavedTicks);
        return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
      }
    }

    private void SetDiskState(long indexBytes, IEnumerable<int> arenaBytes) {
      Interlocked.Exchange(ref _sizeOnDiskBytes, indexBytes + arenaBytes.Sum(x => (long)x));
      Interlocked.Exchange(ref _lastSavedTicks, File.GetLastWriteTimeUtc(_indexPath).Ticks);
    }

    /// <summary>
    /// Compaction (during a write): arena files with less than this fraction of live bytes are
    /// compacted...
    /// </summary>
    public const double CompactionMinLiveFraction = 0.5;

    /// <summary>
    /// ... and when more than this fraction of the arena bytes is dead, the sparsest ones until less
    /// than <see cref="CompactionTargetDeadFraction"/> is dead (hysteresis: not a few files at each
    /// save around the threshold).
    /// </summary>
    public const double CompactionMaxDeadFraction = 0.25;

    public const double CompactionTargetDeadFraction = 0.10;

    /// <summary>The default limit of the bytes copied by the compaction of a write (a write stays short).</summary>
    public const long DefaultCompactionMaxBytes = 1024L * 1024 * 1024;

    /// <summary>Test only: the limit of the bytes copied by a compaction, in MB (0: no compaction).</summary>
    public const string CompactionMaxBytesVariable = "VSCHROMIUM_TEST_CACHE_COMPACTION_MAX_MB";

    /// <summary>The limit of the bytes copied by the compaction of a write (0: no compaction).</summary>
    public long CompactionMaxBytes { get; set; } = GetDefaultCompactionMaxBytes();

    private static long GetDefaultCompactionMaxBytes() {
      return long.TryParse(Environment.GetEnvironmentVariable(CompactionMaxBytesVariable), out var mb) && mb >= 0
        ? mb * 1024 * 1024
        : DefaultCompactionMaxBytes;
    }

    public ProjectIndexCache(string cacheRootDirectory, string rootPath) {
      _rootPath = rootPath;
      _caseSensitive = PathComparerRegistry.Default.Comparison == StringComparison.Ordinal;
      Directory = Path.Combine(cacheRootDirectory, GetProjectDirectoryName(rootPath, _caseSensitive));
      _arenasDirectory = Path.Combine(Directory, IndexCacheFile.ArenasDirectoryName);
      _indexPath = Path.Combine(Directory, IndexCacheFile.IndexFileName);
    }

    /// <summary>The directory of the cache of this project.</summary>
    public string Directory { get; }

    public string RootPath => _rootPath;

    /// <summary>True if this server writes the cache (it holds the lock file).</summary>
    public bool IsWritable => _lockFile != null;

    /// <summary>
    /// The entries of the cache loaded by <see cref="Open"/>, until the end of the first full load
    /// that uses them (<see cref="ReleaseLookup"/>), or null: the contents of the next loads are in
    /// memory (the writes don't need the entries).
    /// </summary>
    public IndexCacheLookup Lookup => _lookup;

    /// <summary>
    /// Releases the entries loaded by <see cref="Open"/> (their memory) after a load completed.
    /// </summary>
    public void ReleaseLookup() {
      _lookup = null;
    }

    /// <summary>
    /// The tree of the project from the cache (phase 2), if the cache has one (loaded by <see
    /// cref="Open"/>, not released yet) and it was written with the same project configuration
    /// (<paramref name="versionHash"/>), otherwise null.
    /// </summary>
    public CachedProjectTree TryLoadTree(string versionHash, IFileSystemNameFactory nameFactory, FullPath rootPath,
                                         CancellationToken cancellationToken) {
      var lookup = _lookup;
      if (lookup == null)
        return null;
      if (!string.Equals(lookup.Index.VersionHash, versionHash, StringComparison.Ordinal)) {
        Logger.LogInfo("The tree of the index cache of \"{0}\" is not used: the project configuration changed.", _rootPath);
        return null;
      }
      var stopwatch = Stopwatch.StartNew();
      var tree = CachedTreeBuilder.Build(lookup.Index, nameFactory, rootPath, cancellationToken);
      Logger.LogInfo("Loaded the tree of \"{0}\" from the index cache in {1:n0} ms: {2:n0} directories, {3:n0} files ({4:n0} without contents).",
        _rootPath, stopwatch.ElapsedMilliseconds, lookup.Index.Directories.Length, lookup.Index.Files.Length,
        tree.FilesWithoutContents.Count);
      return tree;
    }

    /// <summary>
    /// The name of the directory of the cache of a project: a hash of its normalized root path.
    /// </summary>
    public static string GetProjectDirectoryName(string rootPath, bool caseSensitive) {
      var normalized = Path.TrimEndingDirectorySeparator(rootPath);
      if (!caseSensitive) {
        normalized = normalized.ToUpperInvariant();
      }
      var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
      return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    /// <summary>
    /// Opens the cache: takes the lock (if possible), reads the index and maps the arena files.
    /// Never fails because of the contents of the cache (errors are logged, the cache is ignored).
    /// </summary>
    public void Open() {
      System.IO.Directory.CreateDirectory(_arenasDirectory);
      try {
        _lockFile = new FileStream(Path.Combine(Directory, LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1);
      }
      catch (IOException) {
        Logger.LogInfo("The index cache of \"{0}\" is used by another server: read-only.", _rootPath);
      }
      catch (UnauthorizedAccessException) {
        Logger.LogInfo("The index cache of \"{0}\" can't be locked: read-only.", _rootPath);
      }

      if (IsWritable) {
        File.WriteAllText(Path.Combine(Directory, RootFileName), _rootPath);
      }

      IndexCacheIndex data = null;
      List<MappedArena> arenas = null;
      if (File.Exists(_indexPath)) {
        var stopwatch = Stopwatch.StartNew();
        try {
          data = ReadIndex();
          arenas = MapArenas(data);
          Logger.LogInfo("Loaded the index cache of \"{0}\": {1:n0} entries, {2:n0} arena files, in {3:n0} ms.",
            _rootPath, data.Files.Length, data.Arenas.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException) {
          Logger.LogWarn(e, "The index cache of \"{0}\" is invalid: ignored{1}.", _rootPath, IsWritable ? " and deleted" : "");
          data = null;
          arenas = null;
          if (IsWritable) {
            DeleteContents();
          }
        }
      }

      if (IsWritable) {
        // Arena files not referenced by the index: left by an interrupted write.
        foreach (var (id, path) in EnumerateArenaFiles()) {
          if (data == null || !data.Arenas.ContainsKey(id)) {
            TryDeleteFile(path);
          }
          _nextArenaId = Math.Max(_nextArenaId, id + 1);
        }
      }

      if (data != null) {
        lock (_writeLock) {
          foreach (var arena in arenas) {
            _validArenas[arena.Id] = arena.Size;
            _locations.AddOrUpdate(arena.Handle, BlockLocations.ForMappedArena(arena.Id));
          }
          _arenaUsage = ComputeUsage(data);
          SetDiskState(new FileInfo(_indexPath).Length, data.Arenas.Values);
        }
        var stopwatch = Stopwatch.StartNew();
        _lookup = new IndexCacheLookup(data, arenas, _caseSensitive);
        Logger.LogInfo("Built the lookup of the index cache of \"{0}\" in {1:n0} ms ({2:n0} bytes).",
          _rootPath, stopwatch.ElapsedMilliseconds, _lookup.ByteSize);
        StartPreRead(arenas);
      }
    }

    private IndexCacheIndex ReadIndex() {
      using (var stream = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1024 * 1024)) {
        return IndexCacheFile.Read(stream, _rootPath, _caseSensitive);
      }
    }

    private List<MappedArena> MapArenas(IndexCacheIndex data) {
      var result = new List<MappedArena>();
      try {
        foreach (var (id, used) in data.Arenas) {
          var path = Path.Combine(_arenasDirectory, IndexCacheFile.GetArenaFileName(id));
          using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1)) {
            if (stream.Length < used) {
              throw new InvalidDataException($"Arena file \"{path}\" is truncated");
            }
            // Note: The view stays mapped after the file and the mapping are closed.
            using (var mapping = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read,
                     HandleInheritability.None, leaveOpen: true)) {
              var accessor = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
              result.Add(new MappedArena(id, used, accessor));
            }
          }
        }
        return result;
      }
      catch (Exception) {
        // Nothing references the views yet.
        foreach (var arena in result) {
          arena.Accessor.Dispose();
        }
        throw;
      }
    }

    /// <summary>
    /// Reads the mapped arena files in the background (one byte per page), so that the first
    /// searches don't wait for the pages to be read from disk.
    /// </summary>
    private void StartPreRead(List<MappedArena> arenas) {
      var cancellation = new CancellationTokenSource();
      _preReadCancellation = cancellation;
      var token = cancellation.Token;
      Task.Factory.StartNew(() => {
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        try {
          var stopwatch = Stopwatch.StartNew();
          long total = 0;
          foreach (var arena in arenas) {
            if (token.IsCancellationRequested)
              return;
            total += arena.PreRead();
          }
          Logger.LogInfo("Pre-read {0:n0} bytes of the index cache of \"{1}\" in {2:n0} ms.", total, _rootPath,
            stopwatch.ElapsedMilliseconds);
        }
        finally {
          Thread.CurrentThread.Priority = ThreadPriority.Normal;
        }
      }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>
    /// Writes the cache of the project: the tree (<paramref name="directories"/>, depth first) with
    /// the version hash of the project configuration, and the contents of the files: contents
    /// already in arena files keep their location, the others are written to new arena files, then
    /// the index is replaced, and the arena files it doesn't reference are deleted. The metadata of
    /// a file in the tree is the metadata of the read of its contents (e.g. edited files: the scan is
    /// not updated), or of the scan if unknown. The contents of files modified within <see
    /// cref="RacyInterval"/> before they were read, and of files larger than <see
    /// cref="MaxCachedFileLength"/> are not written (the files are in the tree, without contents).
    /// Returns null if this server doesn't write the cache of the project.
    /// </summary>
    public IndexCacheWriteResult Write(IEnumerable<IndexCacheWriteDirectory> directories, string versionHash,
                                       CancellationToken cancellationToken) {
      lock (_writeLock) {
        if (!IsWritable)
          return null;

        var stopwatch = Stopwatch.StartNew();
        var result = new IndexCacheWriteResult();
        var arenaWriter = new ArenaFileWriter(this, result);
        var referencedArenas = new HashSet<int>();
        var liveBytes = new Dictionary<int, long>();
        // The contents in these arena files are copied to new arena files (compaction), decided
        // from the live bytes of the existing arena files in this write (a first pass on the
        // locations of the contents: no I/O).
        var compactionPending = false;
        var compacted = CompactionMaxBytes > 0
          ? SelectArenasToCompact(GetCurrentUsage(directories, cancellationToken), CompactionMaxBytes, _compactionPending,
              out compactionPending)
          : new HashSet<int>();
        var tempPath = Path.Combine(Directory, TempIndexFileName);
        long indexBytes = 0;
        // Note: The entries are written as they are produced (no list of entries in memory).
        var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);
        var completed = false;
        try {
          using (var indexWriter = new IndexCacheFileWriter(stream, _rootPath, versionHash, _caseSensitive)) {
            foreach (var directory in directories) {
              cancellationToken.ThrowIfCancellationRequested();
              indexWriter.AddDirectory(directory.Path, directory.IsSymLink ? IndexCacheDirectoryFlags.SymLink : IndexCacheDirectoryFlags.None);
              foreach (var item in directory.Files) {
                var entry = GetEntry(directory, item, arenaWriter, compacted, result);
                indexWriter.AddFile(item.Name, entry);
                if (!entry.HasNoLocation) {
                  referencedArenas.Add(entry.ArenaId);
                  liveBytes[entry.ArenaId] = liveBytes.GetValueOrDefault(entry.ArenaId) +
                    ArenaAllocator.GetSlotSize(entry.Length + IndexCacheFile.TrailingByteCount);
                  result.EntryCount++;
                } else if (entry.IsBinary) {
                  result.EntryCount++;
                }
              }
            }
            cancellationToken.ThrowIfCancellationRequested();
            arenaWriter.Flush();

            var arenas = referencedArenas.ToDictionary(x => x, x => _validArenas[x]);
            indexWriter.Finish(arenas);
            result.TreeFileCount = indexWriter.FileCount;
            result.TreeDirectoryCount = indexWriter.DirectoryCount;
          }
          stream.Flush(flushToDisk: true);
          result.BytesWritten += stream.Length;
          indexBytes = stream.Length;
          stream.Dispose();
          completed = true;
        }
        finally {
          stream.Dispose();
          if (!completed) {
            TryDeleteFile(tempPath);
          }
        }

        // Replace the index (atomic rename).
        File.Move(tempPath, _indexPath, overwrite: true);

        // Delete the arena files not referenced anymore (may fail if still mapped: they are deleted
        // by a later write, or when the cache is opened next time).
        foreach (var (id, path) in EnumerateArenaFiles()) {
          if (!referencedArenas.Contains(id)) {
            _validArenas.Remove(id);
            if (TryDeleteFile(path)) {
              result.DeletedArenaCount++;
            }
          }
        }

        SetDiskState(indexBytes, referencedArenas.Select(x => _validArenas.GetValueOrDefault(x)));
        result.CompactedArenaCount = compacted.Count;
        _compactionPending = compactionPending;
        _arenaUsage = new IndexCacheArenaUsage(referencedArenas.ToDictionary(x => x, x => (_validArenas.GetValueOrDefault(x), liveBytes[x])));
        result.Elapsed = stopwatch.Elapsed;
        return result;
      }
    }

    /// <summary>
    /// The usage of the existing arena files by the contents of <paramref name="directories"/>
    /// (contents already in arena files).
    /// </summary>
    private IndexCacheArenaUsage GetCurrentUsage(IEnumerable<IndexCacheWriteDirectory> directories, CancellationToken cancellationToken) {
      var live = new Dictionary<int, long>();
      foreach (var directory in directories) {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var item in directory.Files) {
          if (item.Contents is Utf8FileContents text && !text.IsRacy && TryGetLocation(text.Contents, out var arenaId, out _)) {
            live[arenaId] = live.GetValueOrDefault(arenaId) + ArenaAllocator.GetSlotSize(text.ByteLength + IndexCacheFile.TrailingByteCount);
          }
        }
      }
      return new IndexCacheArenaUsage(_validArenas.ToDictionary(x => x.Key, x => (x.Value, live.GetValueOrDefault(x.Key))));
    }

    private static IndexCacheArenaUsage ComputeUsage(IndexCacheIndex index) {
      var live = new Dictionary<int, long>();
      foreach (var file in index.Files) {
        if (!file.ToEntry().HasNoLocation) {
          live[file.ArenaId] = live.GetValueOrDefault(file.ArenaId) + ArenaAllocator.GetSlotSize(file.Length + IndexCacheFile.TrailingByteCount);
        }
      }
      return new IndexCacheArenaUsage(index.Arenas.ToDictionary(x => x.Key, x => (x.Value, live.GetValueOrDefault(x.Key))));
    }

    /// <summary>
    /// The arena files to compact during a write (from the usage of the arena files by the contents
    /// of the write): the files with less than <see cref="CompactionMinLiveFraction"/> of
    /// live bytes, and, when more than <see cref="CompactionMaxDeadFraction"/> of the arena bytes
    /// are dead, the sparsest ones until less than <see cref="CompactionTargetDeadFraction"/> are
    /// dead (hysteresis: the next writes don't compact a few files each),
    /// at most <paramref name="maxBytes"/> of live bytes to copy. <paramref name="resume"/>: the
    /// previous compaction stopped at the limit (the sparsest ones until less than <see
    /// cref="CompactionTargetDeadFraction"/> are dead). <paramref name="pending"/>: this one stops
    /// at the limit.
    /// </summary>
    public static HashSet<int> SelectArenasToCompact(IndexCacheArenaUsage usage, long maxBytes, bool resume, out bool pending) {
      var result = new HashSet<int>();
      pending = false;
      if (maxBytes <= 0 || usage.ArenaCount == 0)
        return result;
      long used = usage.UsedBytes;
      long live = usage.LiveBytes;
      long budget = maxBytes;
      var compactDead = used > 0 && (resume || (used - live) > used * CompactionMaxDeadFraction);
      foreach (var (id, arena) in usage.Arenas.OrderBy(x => (double)x.Value.Live / Math.Max(1, x.Value.Used)).ThenBy(x => x.Key)) {
        var sparse = arena.Live < arena.Used * CompactionMinLiveFraction;
        var tooMuchDead = compactDead && used > 0 && (used - live) > used * CompactionTargetDeadFraction;
        if (!sparse && !tooMuchDead)
          break;
        if (arena.Live > budget)
          break;
        result.Add(id);
        budget -= arena.Live;
        // After the compaction: the live bytes are in new (full) arena files.
        used = used - arena.Used + arena.Live;
      }
      pending = compactDead && (used - live) > used * CompactionTargetDeadFraction;
      return result;
    }

    public static HashSet<int> SelectArenasToCompact(IndexCacheArenaUsage usage, long maxBytes) {
      return SelectArenasToCompact(usage, maxBytes, resume: false, out _);
    }

    /// <summary>
    /// The entry of a file of the tree: its contents (in an arena file), or no contents if they
    /// can't be cached (see <see cref="Write"/>), or not searchable.
    /// </summary>
    private IndexCacheEntry GetEntry(IndexCacheWriteDirectory directory, IndexCacheWriteItem item,
                                     ArenaFileWriter arenaWriter, HashSet<int> compacted, IndexCacheWriteResult result) {
      var contents = item.Contents;
      var metadata = item.Metadata;
      if (contents == null)
        return IndexCacheEntry.WithoutContents(metadata, IndexCacheEntryFlags.NotSearchable);
      if (contents.FileLength < 0) {
        // The metadata of the read are unknown (e.g. read error): the contents are saved only if
        // their time is the time of the scan (and the size of binary files the size of the scan).
        if (contents.UtcLastModified != metadata.LastWriteTimeUtc ||
            (contents is BinaryFileContents && ((BinaryFileContents)contents).BinaryFileSize != metadata.Length)) {
          result.ChangedFileCount++;
          return IndexCacheEntry.WithoutContents(metadata, IndexCacheEntryFlags.NoContents);
        }
      } else if (contents.UtcLastModified != metadata.LastWriteTimeUtc || contents.FileLength != metadata.Length) {
        // The contents were read after the scan (e.g. edited files: the scan is not updated), or
        // the directory entry of the scan is stale (Windows: hard links modified through another
        // name, files being written, file symbolic links). The tree stores the metadata of the
        // read (the metadata of these contents).
        result.ChangedFileCount++;
        AddChangedFileSample(result, directory, item);
        metadata = new FileMetadata(contents.UtcLastModified, contents.FileLength);
      }
      var ticks = metadata.LastWriteTimeUtc.Ticks;
      if (contents.IsRacy) {
        result.RacyFileCount++;
        return IndexCacheEntry.WithoutContents(metadata, IndexCacheEntryFlags.NoContents);
      }

      if (contents is BinaryFileContents) {
        return IndexCacheEntry.WithoutContents(metadata, IndexCacheEntryFlags.Binary);
      }
      if (contents is Utf8FileContents text) {
        if (metadata.Length > MaxCachedFileLength || text.ByteLength > MaxCachedFileLength) {
          result.LargeFileCount++;
          return IndexCacheEntry.WithoutContents(metadata, IndexCacheEntryFlags.NoContents);
        }
        var located = TryGetLocation(text.Contents, out var arenaId, out var offset);
        if (located && compacted.Contains(arenaId)) {
          // Compaction: the live slot is copied (from the contents in memory: the mapped view of
          // the arena file, or the native memory).
          located = false;
          result.CompactedBytes += ArenaAllocator.GetSlotSize(text.ByteLength + IndexCacheFile.TrailingByteCount);
        } else if (!located) {
          result.NewFileCount++;
        }
        if (!located) {
          (arenaId, offset) = arenaWriter.Append(text.Contents);
        }
        return new IndexCacheEntry(ticks, metadata.Length,
          text.IsAscii ? IndexCacheEntryFlags.Ascii : IndexCacheEntryFlags.None, arenaId, offset, text.ByteLength);
      }
      return IndexCacheEntry.WithoutContents(metadata, IndexCacheEntryFlags.NoContents);
    }

    private const int MaxChangedFileSamples = 10;

    /// <summary>
    /// Records a sample of a file skipped because its scan time is not the time of its contents
    /// (diagnostics: what these files are, see <see cref="DescribeChangedFile"/>).
    /// </summary>
    private void AddChangedFileSample(IndexCacheWriteResult result, IndexCacheWriteDirectory directory, IndexCacheWriteItem item) {
      if (result.ChangedFileSamples.Count >= MaxChangedFileSamples)
        return;
      result.ChangedFileSamples.Add(DescribeChangedFile(directory, item));
    }

    private string DescribeChangedFile(IndexCacheWriteDirectory directory, IndexCacheWriteItem item) {
      var relativePath = directory.Path.Length == 0 ? item.Name : directory.Path + PathHelpers.DirectorySeparatorChar + item.Name;
      var text = new StringBuilder();
      text.Append('"').Append(relativePath).Append("\": scan ").Append(item.Metadata.LastWriteTimeUtc.ToString("O"))
        .Append(' ').Append(item.Metadata.Length).Append(" bytes, contents ").Append(item.Contents.UtcLastModified.ToString("O"))
        .Append(' ').Append(item.Contents.FileLength).Append(" bytes");
      try {
        var info = new FileInfo(Path.Combine(_rootPath, relativePath));
        text.Append(", now ").Append(info.LastWriteTimeUtc.ToString("O")).Append(' ').Append(info.Exists ? info.Length : -1)
          .Append(" bytes, attributes ").Append(info.Attributes);
        if (info.LinkTarget != null) {
          text.Append(", link to \"").Append(info.LinkTarget).Append('"');
        }
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        text.Append(", ").Append(e.Message);
      }
      return text.ToString();
    }

    /// <summary>
    /// Deletes the cache (e.g. "Rebuild index"): the next loads read the files from disk. The
    /// contents in memory are written to new arena files by the next write.
    /// </summary>
    public void Invalidate() {
      lock (_writeLock) {
        _lookup = null;
        _locations = new ConditionalWeakTable<SafeHandle, BlockLocations>();
        _validArenas.Clear();
        _arenaUsage = IndexCacheArenaUsage.Empty;
        _compactionPending = false;
        Interlocked.Exchange(ref _sizeOnDiskBytes, 0);
        Interlocked.Exchange(ref _lastSavedTicks, 0);
        if (IsWritable) {
          DeleteContents();
        }
      }
    }

    private void DeleteContents() {
      TryDeleteFile(_indexPath);
      foreach (var (_, path) in EnumerateArenaFiles()) {
        TryDeleteFile(path);
      }
    }

    private IEnumerable<(int Id, string Path)> EnumerateArenaFiles() {
      string[] files;
      try {
        files = System.IO.Directory.GetFiles(_arenasDirectory, "*" + IndexCacheFile.ArenaExtension);
      }
      catch (IOException) {
        yield break;
      }
      foreach (var path in files) {
        var id = IndexCacheFile.ParseArenaFileName(Path.GetFileName(path));
        if (id > 0) {
          yield return (id, path);
        }
      }
    }

    private static bool TryDeleteFile(string path) {
      try {
        File.Delete(path);
        return true;
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        Logger.LogInfo("Cannot delete \"{0}\" from the index cache (in use): {1}", path, e.Message);
        return false;
      }
    }

    private bool TryGetLocation(FileContentsMemory memory, out int arenaId, out int offset) {
      arenaId = 0;
      offset = 0;
      var block = memory.Block;
      if (block == null || !_locations.TryGetValue(block, out var locations))
        return false;
      if (!locations.TryFind(memory.Offset, out arenaId, out offset))
        return false;
      return _validArenas.ContainsKey(arenaId);
    }

    /// <summary>
    /// True if <paramref name="view"/> is the view of an arena file of this cache (not deleted
    /// since it was mapped, see <see cref="Invalidate"/>). Thread safe.
    /// </summary>
    public bool IsMappedArena(SafeMemoryMappedViewHandle view) {
      return _locations.TryGetValue(view, out _);
    }

    private int AllocateArenaId() {
      while (File.Exists(Path.Combine(_arenasDirectory, IndexCacheFile.GetArenaFileName(_nextArenaId)))) {
        _nextArenaId++;
      }
      return _nextArenaId++;
    }

    /// <summary>True if the cache has an index (it was saved).</summary>
    public bool HasIndex => File.Exists(_indexPath);

    /// <summary>
    /// Note: Doesn't wait for a running write (no save at shutdown: it is cancelled by the caller,
    /// and the previous index stays valid).
    /// </summary>
    public void Dispose() {
      _preReadCancellation?.Cancel();
      Interlocked.Exchange(ref _lockFile, null)?.Dispose();
    }

    /// <summary>
    /// Writes the new contents of a write to new arena files (<see cref="IndexCacheFile.ArenaSize"/>
    /// bytes at most), and records their location once an arena file is complete.
    /// </summary>
    private sealed class ArenaFileWriter {
      private readonly ProjectIndexCache _cache;
      private readonly IndexCacheWriteResult _result;
      private readonly List<(SafeHandle Block, int Offset, int ArenaOffset)> _pending = new List<(SafeHandle, int, int)>();
      private byte[] _buffer;
      private int _id = -1;
      private int _used;

      public ArenaFileWriter(ProjectIndexCache cache, IndexCacheWriteResult result) {
        _cache = cache;
        _result = result;
      }

      public unsafe (int ArenaId, int Offset) Append(FileContentsMemory memory) {
        var length = memory.ByteLength;
        var slotSize = ArenaAllocator.GetSlotSize(length + IndexCacheFile.TrailingByteCount);
        if (_id < 0 || _used + slotSize > IndexCacheFile.ArenaSize) {
          Flush();
          _buffer ??= new byte[IndexCacheFile.ArenaSize];
          _id = _cache.AllocateArenaId();
          _used = 0;
        }
        var offset = _used;
        new ReadOnlySpan<byte>(memory.Pointer.ToPointer(), length).CopyTo(_buffer.AsSpan(offset));
        _buffer.AsSpan(offset + length, slotSize - length).Clear();
        _used += slotSize;
        _pending.Add((memory.Block, memory.Offset, offset));
        return (_id, offset);
      }

      public void Flush() {
        if (_id < 0)
          return;
        var path = Path.Combine(_cache._arenasDirectory, IndexCacheFile.GetArenaFileName(_id));
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1)) {
          stream.Write(_buffer, 0, _used);
          stream.Flush(flushToDisk: true);
        }
        _result.NewArenaCount++;
        _result.BytesWritten += _used;
        _cache.RegisterArena(_id, _used, _pending);
        _pending.Clear();
        _id = -1;
        _used = 0;
      }
    }

    private void RegisterArena(int id, int used, List<(SafeHandle Block, int Offset, int ArenaOffset)> pending) {
      _validArenas[id] = used;
      foreach (var group in pending.GroupBy(x => x.Block, ReferenceEqualityComparer.Instance)) {
        var block = (SafeHandle)group.Key;
        _locations.TryGetValue(block, out var existing);
        _locations.AddOrUpdate(block, BlockLocations.Merge(existing, id, group.Select(x => (x.Offset, x.ArenaOffset))));
      }
    }

    /// <summary>
    /// The locations in arena files of the contents of a memory block: a mapped arena file (same
    /// offsets), or the slots of a native arena written to arena files (sorted by offset).
    /// </summary>
    private sealed class BlockLocations {
      private readonly int _mappedArenaId;
      private readonly (int Offset, int ArenaId, int ArenaOffset)[] _slots;

      private BlockLocations(int mappedArenaId, (int, int, int)[] slots) {
        _mappedArenaId = mappedArenaId;
        _slots = slots;
      }

      public static BlockLocations ForMappedArena(int id) {
        return new BlockLocations(id, null);
      }

      /// <summary>
      /// The locations of <paramref name="existing"/> (if they are slots), and <paramref name="slots"/>
      /// in arena file <paramref name="arenaId"/>, which replace the existing locations of the same
      /// contents (compaction: the contents are copied from their previous arena file).
      /// </summary>
      public static BlockLocations Merge(BlockLocations existing, int arenaId, IEnumerable<(int Offset, int ArenaOffset)> slots) {
        var locations = new Dictionary<int, (int ArenaId, int ArenaOffset)>();
        if (existing?._slots != null) {
          foreach (var slot in existing._slots) {
            locations[slot.Offset] = (slot.ArenaId, slot.ArenaOffset);
          }
        }
        foreach (var slot in slots) {
          locations[slot.Offset] = (arenaId, slot.ArenaOffset);
        }
        var result = locations.Select(x => (x.Key, x.Value.ArenaId, x.Value.ArenaOffset)).ToArray();
        Array.Sort(result, (x, y) => x.Item1.CompareTo(y.Item1));
        return new BlockLocations(0, result);
      }

      public bool TryFind(int offset, out int arenaId, out int arenaOffset) {
        if (_mappedArenaId > 0) {
          arenaId = _mappedArenaId;
          arenaOffset = offset;
          return true;
        }
        int low = 0, high = _slots.Length - 1;
        while (low <= high) {
          var middle = low + (high - low) / 2;
          var slot = _slots[middle];
          if (slot.Offset == offset) {
            arenaId = slot.ArenaId;
            arenaOffset = slot.ArenaOffset;
            return true;
          }
          if (slot.Offset < offset) {
            low = middle + 1;
          } else {
            high = middle - 1;
          }
        }
        arenaId = 0;
        arenaOffset = 0;
        return false;
      }
    }
  }

  /// <summary>
  /// An arena file mapped in memory (read-only).
  /// </summary>
  public sealed class MappedArena {
    public MappedArena(int id, int size, MemoryMappedViewAccessor accessor) {
      Id = id;
      Size = size;
      Accessor = accessor;
      Handle = accessor.SafeMemoryMappedViewHandle;
    }

    public int Id { get; }
    /// <summary>The bytes used in the arena file.</summary>
    public int Size { get; }
    /// <summary>
    /// Note: Never disposed once file contents reference the view (that would unmap it): the view
    /// is unmapped when its handle is released (finalized).
    /// </summary>
    public MemoryMappedViewAccessor Accessor { get; }
    public SafeMemoryMappedViewHandle Handle { get; }

    /// <summary>Reads one byte per page; returns the number of bytes.</summary>
    public unsafe long PreRead() {
      var addRef = false;
      try {
        Handle.DangerousAddRef(ref addRef);
        var pointer = (byte*)Handle.DangerousGetHandle().ToPointer();
        var sum = 0;
        for (var offset = 0; offset < Size; offset += 4096) {
          sum += pointer[offset];
        }
        GC.KeepAlive(sum);
        return Size;
      }
      finally {
        if (addRef) {
          Handle.DangerousRelease();
        }
      }
    }
  }
}
