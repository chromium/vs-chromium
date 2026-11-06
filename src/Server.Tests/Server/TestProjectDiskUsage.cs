// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;
using VsChromium.Core.Utility;
using VsChromium.Protocol.Generated;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Operations;
using VsChromium.Server.Projects;
using VsChromium.Server.Search;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestProjectDiskUsage {
    [TestMethod]
    public async Task AggregatesSizesListsExcludedEntriesAndMeasuresAndCancelsExcludedDirectories() {
      var arena = new ArenaAllocator(arenaSize: 64 * 1024, largeAllocationSize: 16 * 1024);
      var factory = new FileSystemNameFactory();
      var rootPath = new FullPath(Path.Combine(Path.GetTempPath(), "disk-usage-unit-root"));
      var projectFilePath = rootPath.Combine(new RelativePath(ConfigurationFileNames.ProjectFileName));

      var ignorePaths = new ConfigurationSectionContents(
        projectFilePath,
        ConfigurationSectionNames.SourceExplorerIgnore,
        new[] { "\\out\\".Replace('\\', Path.DirectorySeparatorChar), "\\slow\\".Replace('\\', Path.DirectorySeparatorChar), "*.suo" });
      var ignoreSearchable = new ConfigurationSectionContents(
        projectFilePath,
        ConfigurationSectionNames.SearchableFilesIgnore,
        Array.Empty<string>());
      var includeSearchable = new ConfigurationSectionContents(
        projectFilePath,
        ConfigurationSectionNames.SearchableFilesInclude,
        new[] { "*" });

      var project = new Project(
        rootPath,
        ignorePaths,
        ignoreSearchable,
        includeSearchable,
        new FileFilter(ignorePaths),
        new DirectoryFilter(ignorePaths),
        new SearchableFilesFilter(ignoreSearchable, includeSearchable),
        "hash-1");

      var rootDirName = factory.CreateAbsoluteDirectoryName(rootPath);
      var srcDirName = factory.CreateDirectoryName(rootDirName, "src");
      var subDirName = factory.CreateDirectoryName(srcDirName, "sub");
      var docsDirName = factory.CreateDirectoryName(rootDirName, "docs");

      var utilFile = factory.CreateFileName(subDirName, "util.h");
      var logoFile = factory.CreateFileName(srcDirName, "logo.png");
      var mainFile = factory.CreateFileName(srcDirName, "main.cc");
      var emptyFile = factory.CreateFileName(docsDirName, "empty.txt");
      var readmeFile = factory.CreateFileName(docsDirName, "readme.md");

      var now = DateTime.UtcNow;
      var subSnap = new DirectorySnapshot(
        new DirectoryData(subDirName, isSymLink: false),
        Array.Empty<DirectorySnapshot>(),
        new[] { utilFile },
        new[] { new FileMetadata(now, 400) });
      var srcSnap = new DirectorySnapshot(
        new DirectoryData(srcDirName, isSymLink: false),
        new[] { subSnap },
        new[] { logoFile, mainFile },
        new[] { new FileMetadata(now, 5000), new FileMetadata(now, 1000) });
      var docsSnap = new DirectorySnapshot(
        new DirectoryData(docsDirName, isSymLink: false),
        Array.Empty<DirectorySnapshot>(),
        new[] { emptyFile, readmeFile },
        new[] { new FileMetadata(now, 0), new FileMetadata(now, 200) });
      var rootSnap = new DirectorySnapshot(
        new DirectoryData(rootDirName, isSymLink: false),
        new[] { docsSnap, srcSnap },
        Array.Empty<FileName>(),
        Array.Empty<FileMetadata>());

      var fsSnapshot = new FileSystemSnapshot(
        version: 7,
        projectRoots: new ReadOnlyCollection<ProjectRootSnapshot>(new[] { new ProjectRootSnapshot(project, rootSnap) }));

      var dbNames = new[] { logoFile, mainFile, utilFile, emptyFile, readmeFile };
      var dbContents = new FileContents[] {
        new BinaryFileContents(now, 5000),
        CreateUtf8(arena, "main", fileLength: 1000),
        CreateUtf8(arena, "util", fileLength: 400),
        BinaryFileContents.Empty,
        CreateUtf8(arena, "readme", fileLength: 200),
      };
      var db = new FileDatabaseSnapshot(
        new Dictionary<FullPath, string>(),
        new Dictionary<DirectoryName, FileRange> {
          [srcDirName] = new FileRange(0, 2),
          [subDirName] = new FileRange(2, 1),
          [docsDirName] = new FileRange(3, 2),
        },
        dbNames,
        dbContents);

      var fs = new FakeDiskFileSystem();
      fs.SetDirectory(rootPath,
        Dir("docs"),
        Dir("src"),
        Dir("out"),
        Dir("slow"),
        FileEntry("state.suo", 3000));
      fs.SetDirectory(rootPath.Combine(new RelativePath("src")),
        Dir("sub"),
        FileEntry("logo.png", 5000),
        FileEntry("main.cc", 1000));
      fs.SetDirectory(rootPath.Combine(new RelativePath("out")),
        Dir("nested"),
        SymLinkDir("loop"),
        FileEntry("app.exe", 20_000));
      fs.SetDirectory(rootPath.Combine(new RelativePath("out").CreateChild("nested")),
        FileEntry("lib.dll", 10_000));
      fs.SetDirectory(rootPath.Combine(new RelativePath("out").CreateChild("loop")),
        FileEntry("ignored_in_symlink.bin", 999_999));

      using var slowGate = new ManualResetEventSlim(false);
      using var slowEntered = new ManualResetEventSlim(false);
      var slowPath = rootPath.Combine(new RelativePath("slow"));
      fs.SetDirectoryCallback(slowPath, () => {
        slowEntered.Set();
        slowGate.Wait(TimeSpan.FromSeconds(5));
        return new[] { FileEntry("part1.bin", 4096) };
      });

      var snapshotManager = new StubSnapshotManager(fsSnapshot);
      var searchEngine = new StubSearchEngine(db);
      using var service = new ProjectDiskUsageService(fs, snapshotManager, searchEngine);

      // Root sizes before measuring excluded directories.
      var rootSizes = service.GetDirectorySizes(new DirectoryPathParams {
        ProjectPath = rootPath.Value,
        RelativePath = "",
      });
      Assert.AreEqual("", rootSizes.RelativePath);
      Assert.AreEqual(projectFilePath.Value, rootSizes.ProjectFile);
      Assert.AreEqual(7, rootSizes.SnapshotVersion);
      Assert.AreEqual(6600L, rootSizes.Totals.TotalBytes);
      Assert.AreEqual(1600L, rootSizes.Totals.SearchableBytes);
      Assert.AreEqual(5000L, rootSizes.Totals.NotSearchableBytes);
      Assert.AreEqual(5L, rootSizes.Totals.FileCount);
      Assert.AreEqual(3L, rootSizes.Totals.SearchableFileCount);
      Assert.IsNull(rootSizes.Totals.Pattern);

      // Order: non-zero sized entries descending (src 6400, state.suo 3000, docs 200), then unmeasured excluded dirs (out, slow).
      CollectionAssert.AreEqual(
        new[] { "src", "state.suo", "docs", "out", "slow" },
        rootSizes.Children.Select(c => c.Name).ToArray());

      var suoEntry = rootSizes.Children.Single(c => c.Name == "state.suo");
      Assert.IsTrue(suoEntry.Excluded);
      Assert.IsFalse(suoEntry.IsDirectory);
      Assert.AreEqual(MeasureState.Done, suoEntry.MeasureState);
      Assert.AreEqual(3000L, suoEntry.TotalBytes);
      Assert.AreEqual("/state.suo", suoEntry.Pattern);

      var outBefore = rootSizes.Children.Single(c => c.Name == "out");
      Assert.IsTrue(outBefore.Excluded);
      Assert.AreEqual(MeasureState.NotMeasured, outBefore.MeasureState);
      Assert.AreEqual("/out/", outBefore.Pattern);

      // Second query reuses cached aggregation (ComputeMs == 0).
      var srcSizes = service.GetDirectorySizes(new DirectoryPathParams {
        ProjectPath = rootPath.Value,
        RelativePath = "src",
      });
      Assert.AreEqual(0.0, srcSizes.ComputeMs);
      Assert.AreEqual("src", srcSizes.RelativePath);
      Assert.AreEqual("/src/", srcSizes.Totals.Pattern);
      CollectionAssert.AreEqual(
        new[] { "logo.png", "main.cc", "sub" },
        srcSizes.Children.Select(c => c.Name).ToArray());
      Assert.AreEqual("/src/sub/", srcSizes.Children.Single(c => c.Name == "sub").Pattern);

      // Measure excluded "out": skips symlink "loop" and sums 20,000 + 10,000 = 30,000.
      var measured = await service.MeasureDirectoryAsync(new MeasureDirectoryParams {
        ProjectPath = rootPath.Value,
        RelativePath = "out",
      }, CancellationToken.None);
      if (measured.State == MeasureState.Running) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (measured.State == MeasureState.Running && DateTime.UtcNow < deadline) {
          await Task.Delay(15);
          measured = await service.MeasureDirectoryAsync(new MeasureDirectoryParams {
            ProjectPath = rootPath.Value,
            RelativePath = "out",
          }, CancellationToken.None);
        }
      }
      Assert.AreEqual(MeasureState.Done, measured.State);
      Assert.AreEqual(30_000L, measured.TotalBytes);
      Assert.AreEqual(2L, measured.FileCount);

      // Now "out" sorts first (30,000 B > src 6,400 B), while indexed Totals remains 6,600 B.
      var rootSizesAfter = service.GetDirectorySizes(new DirectoryPathParams {
        ProjectPath = rootPath.Value,
        RelativePath = "",
      });
      Assert.AreEqual(6600L, rootSizesAfter.Totals.TotalBytes);
      Assert.AreEqual("out", rootSizesAfter.Children[0].Name);
      Assert.AreEqual(30_000L, rootSizesAfter.Children[0].TotalBytes);
      Assert.AreEqual(MeasureState.Done, rootSizesAfter.Children[0].MeasureState);

      // Start measuring "slow", verify Running state, then cancel it.
      var runningMeasure = await service.MeasureDirectoryAsync(new MeasureDirectoryParams {
        ProjectPath = rootPath.Value,
        RelativePath = "slow",
      }, CancellationToken.None);
      Assert.IsTrue(slowEntered.Wait(TimeSpan.FromSeconds(2)));
      Assert.AreEqual(MeasureState.Running, runningMeasure.State);

      var cancelled = service.CancelMeasure(new DirectoryPathParams {
        ProjectPath = rootPath.Value,
        RelativePath = "slow",
      });
      slowGate.Set();
      Assert.AreEqual(MeasureState.Cancelled, cancelled.State);

      // Measuring an already-indexed directory is rejected.
      await Assert.ThrowsExactlyAsync<RecoverableErrorException>(async () => {
        await service.MeasureDirectoryAsync(new MeasureDirectoryParams {
          ProjectPath = rootPath.Value,
          RelativePath = "src",
        }, CancellationToken.None);
      });
    }

    [TestMethod]
    public void ChromiumProjectWithoutLocalProjectFileDisablesIgnoreEditing() {
      var factory = new FileSystemNameFactory();
      var rootPath = new FullPath(Path.Combine(Path.GetTempPath(), "chromium-root"));
      var externalConfigFile = new FullPath(Path.Combine(Path.GetTempPath(), "builtin-config", "ChromiumExplorer.ignore"));

      var emptySection = new ConfigurationSectionContents(externalConfigFile, "", Array.Empty<string>());
      var includeAll = new ConfigurationSectionContents(externalConfigFile, "", new[] { "*" });
      var project = new Project(
        rootPath,
        emptySection,
        emptySection,
        includeAll,
        new FileFilter(emptySection),
        new DirectoryFilter(emptySection),
        new SearchableFilesFilter(emptySection, includeAll),
        "hash-cr");

      var rootSnap = new DirectorySnapshot(
        new DirectoryData(factory.CreateAbsoluteDirectoryName(rootPath), isSymLink: false),
        Array.Empty<DirectorySnapshot>(),
        Array.Empty<FileName>(),
        Array.Empty<FileMetadata>());
      var fsSnapshot = new FileSystemSnapshot(
        1,
        new ReadOnlyCollection<ProjectRootSnapshot>(new[] { new ProjectRootSnapshot(project, rootSnap) }));

      var fs = new FakeDiskFileSystem();
      fs.SetDirectory(rootPath);
      using var service = new ProjectDiskUsageService(
        fs,
        new StubSnapshotManager(fsSnapshot),
        new StubSearchEngine(FileDatabaseSnapshot.Empty()));

      var sizes = service.GetDirectorySizes(new DirectoryPathParams {
        ProjectPath = rootPath.Value,
        RelativePath = "",
      });
      Assert.IsNull(sizes.ProjectFile);

      Assert.ThrowsExactly<RecoverableErrorException>(() => service.AddIgnorePattern(new AddIgnorePatternParams {
        ProjectPath = rootPath.Value,
        Section = IgnoreSection.SourceExplorerIgnore,
        Pattern = "/out/",
      }));
    }

    /// <summary>
    /// Symbolic links: listed with their target, not counted in the sizes (file links, and the files
    /// seen through a directory link).
    /// </summary>
    [TestMethod]
    public void SymbolicLinksAreNotCountedAndLinkToTheirTarget() {
      var factory = new FileSystemNameFactory();
      var rootPath = new FullPath(Path.Combine(Path.GetTempPath(), "disk-usage-links-root"));
      var projectFilePath = rootPath.Combine(new RelativePath(ConfigurationFileNames.ProjectFileName));
      var none = new ConfigurationSectionContents(projectFilePath, ConfigurationSectionNames.SourceExplorerIgnore, Array.Empty<string>());
      var include = new ConfigurationSectionContents(projectFilePath, ConfigurationSectionNames.SearchableFilesInclude, new[] { "*" });
      var project = new Project(rootPath, none, none, include, new FileFilter(none), new DirectoryFilter(none),
        new SearchableFilesFilter(none, include), "hash-links");

      // root
      //   src/real.cc (100), src/link.cc -> src/real.cc
      //   dirlink -> src (real.cc seen through the link)
      //   netlink -> \\server\share\dir (data.bin seen through the link)
      //   outside.txt -> <temp>/disk-usage-outside/x.txt (30), dangling.cc -> (none)
      var now = DateTime.UtcNow;
      var rootDirName = factory.CreateAbsoluteDirectoryName(rootPath);
      var srcDirName = factory.CreateDirectoryName(rootDirName, "src");
      var dirLinkName = factory.CreateDirectoryName(rootDirName, "dirlink", true);
      var netLinkName = factory.CreateDirectoryName(rootDirName, "netlink", true);
      var src = new DirectorySnapshot(new DirectoryData(srcDirName, isSymLink: false), Array.Empty<DirectorySnapshot>(),
        new[] { factory.CreateFileName(srcDirName, "link.cc"), factory.CreateFileName(srcDirName, "real.cc") },
        new[] { new FileMetadata(now, 100), new FileMetadata(now, 100) }, new[] { 0 });
      var dirLink = new DirectorySnapshot(new DirectoryData(dirLinkName, isSymLink: true), Array.Empty<DirectorySnapshot>(),
        new[] { factory.CreateFileName(dirLinkName, "real.cc") }, new[] { new FileMetadata(now, 100) });
      var netLink = new DirectorySnapshot(new DirectoryData(netLinkName, isSymLink: true), Array.Empty<DirectorySnapshot>(),
        new[] { factory.CreateFileName(netLinkName, "data.bin") }, new[] { new FileMetadata(now, 50) });
      var root = new DirectorySnapshot(new DirectoryData(rootDirName, isSymLink: false), new[] { dirLink, netLink, src },
        new[] { factory.CreateFileName(rootDirName, "dangling.cc"), factory.CreateFileName(rootDirName, "outside.txt") },
        new[] { new FileMetadata(now, 0), new FileMetadata(now, 30) }, new[] { 0, 1 });
      var fsSnapshot = new FileSystemSnapshot(1, new ReadOnlyCollection<ProjectRootSnapshot>(new[] { new ProjectRootSnapshot(project, root) }));

      var outsideTarget = Path.Combine(Path.GetTempPath(), "disk-usage-outside", "x.txt");
      var fs = new FakeDiskFileSystem();
      fs.LinkTargets[rootPath.Combine(new RelativePath("src").CreateChild("link.cc")).Value] = rootPath.Combine(new RelativePath("src").CreateChild("real.cc")).Value;
      fs.LinkTargets[rootPath.Combine(new RelativePath("dirlink")).Value] = rootPath.Combine(new RelativePath("src")).Value;
      fs.LinkTargets[rootPath.Combine(new RelativePath("netlink")).Value] = @"\\server\share\dir";
      fs.LinkTargets[rootPath.Combine(new RelativePath("outside.txt")).Value] = outsideTarget;
      using var service = new ProjectDiskUsageService(fs, new StubSnapshotManager(fsSnapshot), new StubSearchEngine(null));

      var sizes = service.GetDirectorySizes(new DirectoryPathParams { ProjectPath = rootPath.Value, RelativePath = "" });
      Assert.AreEqual(100L, sizes.Totals.TotalBytes, "Only src/real.cc: not the links nor the files seen through them");
      Assert.AreEqual(1L, sizes.Totals.FileCount);
      CollectionAssert.AreEqual(new[] { "src", "dirlink", "netlink", "dangling.cc", "outside.txt" }, sizes.Children.Select(x => x.Name).ToList(),
        "The sized entries, then the links");
      var bySize = sizes.Children.ToDictionary(x => x.Name);
      Assert.AreEqual(100L, bySize["src"].TotalBytes, "src/link.cc not counted");
      Assert.IsNull(bySize["src"].SymbolicLink);

      var dirLinkEntry = bySize["dirlink"];
      Assert.AreEqual(0L, dirLinkEntry.TotalBytes);
      Assert.AreEqual(0L, dirLinkEntry.FileCount);
      Assert.AreEqual(SymbolicLinkKind.Directory, dirLinkEntry.SymbolicLink.Kind);
      Assert.AreEqual(rootPath.Combine(new RelativePath("src")).Value, dirLinkEntry.SymbolicLink.Target);
      Assert.AreEqual(rootPath.Value, dirLinkEntry.SymbolicLink.TargetProjectPath);
      Assert.AreEqual("src", dirLinkEntry.SymbolicLink.TargetRelativePath);
      Assert.AreEqual(100L, dirLinkEntry.SymbolicLink.TargetTotalBytes);
      Assert.AreEqual(1L, dirLinkEntry.SymbolicLink.TargetFileCount);
      Assert.IsFalse(dirLinkEntry.SymbolicLink.External || dirLinkEntry.SymbolicLink.Network || dirLinkEntry.SymbolicLink.Dangling);
      Assert.AreEqual("/dirlink/", dirLinkEntry.Pattern, "The pattern of the link itself");

      var net = bySize["netlink"].SymbolicLink;
      Assert.IsTrue(net.Network);
      Assert.IsFalse(net.External);
      Assert.IsNull(net.TargetProjectPath);
      Assert.AreEqual(50L, net.TargetTotalBytes);

      var outside = bySize["outside.txt"].SymbolicLink;
      Assert.AreEqual(SymbolicLinkKind.File, outside.Kind);
      Assert.IsTrue(outside.External);
      Assert.AreEqual(outsideTarget, outside.Target);
      Assert.AreEqual(30L, outside.TargetTotalBytes);
      Assert.AreEqual(0L, bySize["outside.txt"].TotalBytes);

      var dangling = bySize["dangling.cc"].SymbolicLink;
      Assert.IsTrue(dangling.Dangling);
      Assert.IsNull(dangling.TargetTotalBytes);

      var srcSizes = service.GetDirectorySizes(new DirectoryPathParams { ProjectPath = rootPath.Value, RelativePath = "src" });
      var fileLink = srcSizes.Children.Single(x => x.Name == "link.cc");
      Assert.AreEqual(0L, fileLink.TotalBytes);
      Assert.AreEqual("src/real.cc", fileLink.SymbolicLink.TargetRelativePath);
      Assert.AreEqual(100L, fileLink.SymbolicLink.TargetTotalBytes);
      Assert.AreEqual(1L, fileLink.SymbolicLink.TargetFileCount);
      Assert.AreEqual(100L, srcSizes.Totals.TotalBytes);
    }

    private static FileContents CreateUtf8(ArenaAllocator arena, string text, long fileLength) {
      var bytes = Encoding.UTF8.GetBytes(text);
      var slice = arena.Allocate(bytes.Length + 2);
      bytes.CopyTo(slice.Span);
      slice.Span.Slice(bytes.Length).Clear();
      return new Utf8FileContents(
        new FileContentsMemory(slice.Block, slice.Offset, bytes.Length),
        DateTime.UtcNow,
        isAscii: true,
        fileLength: fileLength);
    }

    private static DirectoryEntry Dir(string name) =>
      new DirectoryEntry(name, FileAttributes.Directory);

    private static DirectoryEntry SymLinkDir(string name) =>
      new DirectoryEntry(name, FileAttributes.Directory | FileAttributes.ReparsePoint);

    private static DirectoryEntry FileEntry(string name, long size) =>
      new DirectoryEntry(name, FileAttributes.Normal, new FileMetadata(DateTime.UtcNow, size));

    private sealed class FakeDiskFileSystem : IFileSystem {
      private readonly Dictionary<FullPath, Func<IList<DirectoryEntry>>> _dirs = new Dictionary<FullPath, Func<IList<DirectoryEntry>>>();

      /// <summary>Link full path => final target (null: dangling).</summary>
      public readonly Dictionary<string, string> LinkTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      public string ResolveSymLinkTarget(FullPath path, bool allowCached) =>
        LinkTargets.TryGetValue(path.Value, out var target) ? target : null;

      public void SetDirectory(FullPath path, params DirectoryEntry[] entries) {
        _dirs[path] = () => entries;
      }

      public void SetDirectoryCallback(FullPath path, Func<IList<DirectoryEntry>> callback) {
        _dirs[path] = callback;
      }

      public IFileInfoSnapshot GetFileInfoSnapshot(FullPath path) {
        var isDir = _dirs.ContainsKey(path);
        return new FakeFileInfo(path, exists: isDir, isDirectory: isDir);
      }

      public IList<DirectoryEntry> GetDirectoryEntries(FullPath path) {
        return _dirs.TryGetValue(path, out var fn) ? fn() : Array.Empty<DirectoryEntry>();
      }

      public string ReadText(FullPath path) => throw new NotSupportedException();
      public SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, long fileSize, int trailingByteCount) => throw new NotSupportedException();
      public IFileSystemWatcher CreateDirectoryWatcher(FullPath path) => throw new NotSupportedException();
    }

    private sealed class FakeFileInfo : IFileInfoSnapshot {
      public FakeFileInfo(FullPath path, bool exists, bool isDirectory) {
        Path = path;
        Exists = exists;
        IsDirectory = isDirectory;
      }

      public FullPath Path { get; }
      public bool Exists { get; }
      public bool IsFile => Exists && !IsDirectory;
      public bool IsDirectory { get; }
      public bool IsSymLink => false;
      public DateTime LastWriteTimeUtc => DateTime.UtcNow;
      public long Length => 0;
    }

    private sealed class StubSnapshotManager : IFileSystemSnapshotManager {
      public StubSnapshotManager(FileSystemSnapshot snapshot) {
        CurrentSnapshot = snapshot;
      }

      public FileSystemSnapshot CurrentSnapshot { get; set; }
      public event EventHandler<OperationInfo> SnapshotScanStarted { add { } remove { } }
      public event EventHandler<SnapshotScanResult> SnapshotScanFinished;
      public event EventHandler<FilesChangedEventArgs> FilesChanged { add { } remove { } }
      public event EventHandler<FileSystemWatchPausedEventArgs> FileSystemWatchPaused { add { } remove { } }
      public event EventHandler FileSystemWatchResumed { add { } remove { } }
      public void Pause() { }
      public void Resume() { }
      public void Refresh() { }
      public void RebuildProject(FullPath projectRoot) { }
      public void RaiseScanFinished(SnapshotScanResult r) => SnapshotScanFinished?.Invoke(this, r);
    }

    private sealed class StubSearchEngine : ISearchEngine {
      public StubSearchEngine(IFileDatabaseSnapshot db) {
        CurrentFileDatabaseSnapshot = db;
      }

      public IFileDatabaseSnapshot CurrentFileDatabaseSnapshot { get; set; }
      public event EventHandler<OperationInfo> FilesLoading { add { } remove { } }
      public event EventHandler<OperationInfo> FilesLoadingProgress { add { } remove { } }
      public event EventHandler<FilesLoadedResult> FilesLoaded;
      public IFileDatabaseSnapshot GetCompleteDatabase(int fileSystemSnapshotVersion) => CurrentFileDatabaseSnapshot;
      public VsChromium.Server.Search.SearchCodeResult SearchCode(VsChromium.Server.Search.SearchParams searchParams, CancellationToken cancellationToken = default) => throw new NotSupportedException();
      public SearchFilePathsResult SearchFilePaths(VsChromium.Server.Search.SearchParams searchParams, CancellationToken cancellationToken = default) => throw new NotSupportedException();
      public IEnumerable<VsChromium.Server.Search.FileExtract> GetFileExtracts(FullPath path, IEnumerable<VsChromium.Server.Search.FilePositionSpan> spans, int maxLength) => throw new NotSupportedException();
      public void SetProjectOptions(FullPath path, bool? trigramFiltersEnabled) { }
      public FullPath GetProjectRoot(FullPath path) => path;
      public bool IsTrigramFiltersEnabled(FullPath projectRootPath) => false;
      public long GetTrigramFiltersBytes(FullPath projectRoot) => 0;
      public void RaiseFilesLoaded(FilesLoadedResult r) => FilesLoaded?.Invoke(this, r);
    }
  }
}
