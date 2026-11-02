# Porting the VsChromium server to portable modern .NET

This document records the goals, principles and decisions of the port of the indexing/search
server (`VsChromium.Server.exe`) from .NET Framework 4.7.2 + custom C/C++ (Win32 only) to a
portable modern .NET application (Windows, Linux, macOS). It is meant to be read before working on
the `portable-server` branch, including in a future conversation with no other context.

Status: **planning** (branch `portable-server`, created from `main` at `30d7bb9`).

## Goals

1. **Portable server**: the server runs on Windows, Linux and macOS (x64 and arm64), built from
   a single code base with modern .NET (see [Target framework](#target-framework-and-distribution)).
2. **No custom C/C++**: remove `src/Native` (VsChromium.Native.dll), `src/re2` (2011 RE2 snapshot,
   `third_party/re2-*`) and `src/ServerNativeInterop`. P/Invoke to **OS APIs** is allowed where it
   brings real value (e.g. Linux `getdents64`/`statx`, macOS `getattrlistbulk`), preferably through
   reused, tested code (mtsuite), never through code we compile ourselves.
3. **Reuse mtsuite**: use as much as possible of `C:\src\mtsuite` (same author, Apache 2.0, .NET 8,
   NativeAOT-compatible, cross-platform), in particular `src/core-filesystem` (paths, entries,
   per-OS directory enumeration, object pools) and the parallel traversal of `src/shared`.
4. **Same or better performance**, proven by a benchmark suite (see [Benchmarks](#benchmarks))
   measuring **CPU usage, memory usage (managed _and_ native) and response time**, run on the
   current server (baseline) and on the ported server.
5. **One server for both clients**: the Visual Studio extension (net472, runs inside devenv.exe)
   and the VS Code extension (TypeScript) both use the new server.

## Non-goals

- Porting the Visual Studio extension itself: it stays .NET Framework 4.7.2 (VS requirement),
  Windows only.
- Porting the client-only Windows code (Chrome process attach, debugger, job objects, shell icons,
  registry): it stays in the VS-side assemblies.
- New features. The port must be behavior-compatible (same results), except for the deliberate
  fixes listed in [Behavior changes](#behavior-changes).

## Principles

1. **Clients and server are deployed together.** The VSIX and the VS Code VSIX always ship the
   server they talk to. **Breaking changes to the protocol, message types, file layout and command
   line are allowed**; there is no need for compatibility with previous versions. (The protocol
   must still be identical between the VS and VS Code clients of the same version.)
2. **Measure before and after.** The benchmark suite (see [Benchmarks](#benchmarks)) is written and a
   baseline is recorded on the current server **before** changing the server. Every
   performance-sensitive change is compared to the baseline. Regressions must be understood and
   accepted explicitly, not discovered later.
3. **Correctness parity first, except for bugs.** Search results (hit counts, matched files,
   positions) must match the current server on the benchmark corpus, **except where the current
   behavior is a bug or questionable**: such cases are decided **case by case**, and each accepted
   difference is recorded in [Behavior changes](#behavior-changes) (with the reason) before it is
   relied upon. The existing native tests (`Tests/NativeInterop/TestAsciiSearch`,
   `TestGetTextKind`, `TestGetLineExtent`) are ported as oracles for the managed implementations
   (minus the recorded behavior changes).
4. **Incremental, always green.** Small steps, each building and passing all tests (C#, TypeScript),
   each committed separately. The server keeps working with both clients at every step.
5. **Copy mtsuite code, keep the changes backportable.** mtsuite code is copied into the repository
   with its license and origin commit recorded, and **re-organized as needed** for the server
   (see [Reusing mtsuite](#reusing-mtsuite)). Every change made to it is documented so it can be
   backported to mtsuite, the goal being to turn the mtsuite file system code into a reusable
   library.
6. **Portable by default, OS-specific only when measured.** Use the BCL first
   (`FileSystemEnumerator`, `RandomAccess`, `FileSystemWatcher`, `SearchValues`, `Regex`); add
   OS-specific fast paths only with benchmark evidence (mtsuite's Linux/macOS enumerators qualify).
7. **No Windows assumptions in the server**: path separators, drive letters, case sensitivity
   (case-insensitive on Windows/macOS, case-sensitive on Linux, as in mtsuite `PathHelpers`), line
   endings, file sharing semantics, `\\?\` long paths.
8. **Keep native memory visible.** Any unmanaged allocation (if kept for file contents) must be
   accounted for and reported (logs, `GetDatabaseStatistics`, benchmarks).

## Architecture

### Project split

| Project | Target | Used by | Content |
|---|---|---|---|
| `VsChromium.Core.Ipc` (new) | `netstandard2.0` | VS package (net472), server, tests | `Core/Ipc/**`: message types (`[ProtoContract]`), serializers |
| `VsChromium.Core` (existing, slimmed) | `net472` | VS package, Host, DkmIntegration | Client-only Windows code (processes, debugger, jobs, shell, registry, VS-side file system helpers) |
| `VsChromium.Server` (ported) | modern .NET | server process | All server code + managed replacements for the native code + portable file system |
| `MtSuite.CoreFileSystem` (copied from mtsuite) | modern .NET | server | Paths, entries, per-OS enumeration, object pools, parallel traversal |
| `VsChromium.Server.Tests` (new) | modern .NET | – | Server unit tests + end-to-end tests over stdio |
| `VsChromium.Benchmarks` (new) | modern .NET | – | Black-box benchmark runner (see below) |
| `Tests` (existing) | `net472` | – | VS-side tests only |

Code shared by the VS package and the server today (paths, pattern matching, configuration,
logging) is either moved to a `netstandard2.0` library or duplicated where the server version
diverges (e.g. mtsuite `FullPath` on the server). `src/Native`, `src/re2`,
`src/ServerNativeInterop`, `third_party/re2-*` and the unused P/Invokes are deleted at the end.

All new/ported projects use **SDK-style** csproj.

### Target framework and distribution

- **Decided: target framework `net10.0`** (current LTS; the installed SDK is 10.0.x). mtsuite targets
  `net8.0`; its code is compiled as part of our projects with our target framework.
- **Decided: self-contained publish per runtime identifier** (`win-x64`, `win-arm64`,
  `linux-x64`, `linux-arm64`, `osx-arm64`), so users don't need to install a .NET runtime. The VS
  VSIX bundles `win-x64` (and `win-arm64` if we add ARM64 VS support); the VS Code VSIX is already
  platform-specific (`vsce package --target ...`).
- **NativeAOT is a later goal, not a requirement of the port**: it requires replacing reflection
  based components (MEF v1, Newtonsoft.Json, protobuf-net runtime model). New code should avoid
  needless reflection so that AOT stays possible.

### Process model and protocol

- The server is started **directly** by the client (no `VsChromium.Host.exe`, no debugger
  attachment, no job object). **It exits when its input stream is closed** (client exit or crash),
  as the `--stdio` mode already does.
- **Decided: a single transport for both clients: stdio + JSON lines** (the existing `--stdio`
  mode, `src/Server/Ipc/Json`). The VS package switches from TCP + protobuf-net to stdio + JSON,
  which removes Host.exe, the TCP listener, protobuf-net and the debugger/job-object machinery.
  This is validated by the benchmarks (large search results over JSON vs protobuf); if JSON is too
  slow, keep protobuf-net (upgraded to 3.x, `netstandard2.0`) over stdio for VS.
- The message classes in `VsChromium.Core.Ipc` remain the single definition of the protocol;
  `src/vscode/src/server/messages.ts` mirrors them.

### File system

- **Paths**: mtsuite `FullPath` (immutable class, parent chain, low memory for large trees) and
  `PathHelpers` (OS-dependent case sensitivity), replacing `VsChromium.Core.Files.FullPath` /
  `RelativePath` in the server. Pattern matching (`Core/Files/PatternMatching`, the `.ignore` /
  `.include` / `.patterns` configuration files) is kept (it is managed code) and made separator-
  and case-sensitivity-aware.
- **Directory enumeration**: mtsuite `IFileSystem` / `FileSystemPortable` (BCL
  `FileSystemEnumerator` on Windows, `getdents64` + `statx` on Linux, `getattrlistbulk` on macOS),
  replacing `NtQueryDirectoryFile` (`Core/Win32/Files/NativeFile.cs`).
- **Parallel scanning**: mtsuite `ParallelFileSystem.TraverseDirectoryAsync` + `IDirectorCollector`,
  extended with `CancellationToken` and a **scoped** concurrency limit (no process-wide
  `ThreadPool.SetMin/MaxThreads`, see mtsuite `docs/ParallelFileSystem_Recommendations.md`).
- **File reading**: `File.OpenHandle` + `RandomAccess.Read` with `FileShare.ReadWrite | Delete`
  (files are being edited/built while indexed).
- **Change notifications**: `FileSystemWatcher` (already portable). On Linux, use **one** watcher per
  root (not 3) to limit inotify watches (`fs.inotify.max_user_watches`), with a documented polling /
  rescan fallback when the limit is hit.

### File contents and search

- **Storage**: file contents stay **UTF-8 / ASCII bytes** in memory (half the size of UTF-16).
  Whether they live in GC arrays (`GC.AllocateUninitializedArray`, possibly pinned) or
  `NativeMemory` is decided by benchmarks (GC heap size and pauses vs native memory); either way
  the total is reported.
- **Text kind detection** (ASCII / UTF-8 / binary) and **line extents**: managed ports of
  `Text_GetKind` and `*_GetLineExtentFromPosition`, using vectorized BCL APIs (`Ascii.IsValid`,
  `Utf8.IsValid`... / `Span.IndexOf`).
- **Literal search**: vectorized `ReadOnlySpan<byte>.IndexOf` (mtsuite `mtfindstr` approach) and
  `SearchValues` for case-insensitive search; managed Boyer-Moore/BNDM only if benchmarks show a
  benefit.
- **Regex**: `System.Text.RegularExpressions.Regex` (source-generated/compiled where possible,
  `RegexOptions.NonBacktracking` to keep RE2's linear-time guarantee for user-typed patterns).
  .NET regexes run on `char`, so regex searches decode each file (or chunk) to UTF-16 in a pooled
  per-thread buffer; the cost is measured by the benchmarks.
- **Parallelism**: keep the current partitioned PLINQ model (pieces distributed over
  `ProcessorCount` partitions), with per-thread search state.
- **Positions**: match positions/extract offsets sent to clients are **UTF-16 code unit offsets**
  (what the VS editor and VS Code use), see below.

### Dependencies

- MEF v1 (`System.ComponentModel.Composition`) is available on modern .NET as a NuGet package and is
  **kept initially** to limit churn; replacing it (e.g. Microsoft.Extensions.DependencyInjection) is
  a separate, later step (prerequisite for NativeAOT).
- NLog 4.4 → NLog 5 (or Microsoft.Extensions.Logging), logs still under the local app data folder
  (`%LOCALAPPDATA%\VsChromium` on Windows, `$XDG_STATE_HOME`/`~/.local/state/VsChromium` on Linux,
  `~/Library/Logs/VsChromium` on macOS).
- Newtonsoft.Json for the JSON protocol initially; `System.Text.Json` (source generated) later.

## Behavior changes

Deliberate differences from the current server (see principle 3). Each entry is decided case by
case; add new entries here (with the reason) as they are found during the port. **Every behavior
change comes with regression tests** (server unit tests and, where relevant, end-to-end tests over
stdio) that fail on the current server and pass after the fix. Initial list, found while auditing
the current server (all four are to be fixed):

1. **Positions are byte offsets** for UTF-8 files (`AsciiFileContents` searches raw bytes), while the
   clients interpret them as character offsets: wrong match selection in files with non-ASCII text
   (VS Code `document.positionAt`). The new server reports UTF-16 offsets.
2. **Matches spanning 100 KB file chunk boundaries are missed** (`FileDatabaseBuilder` splits files
   into 100 KB pieces without overlap/newline alignment).
3. **Non-ASCII search patterns are mangled** (`Marshal.StringToHGlobalAnsi`); case-insensitive and
   whole-word matching are ASCII-only.
4. The non-RE2 regex path uses C++ `std::regex` (ECMAScript, backtracking); both clients always
   send `UseRe2Engine=true`. The new server has a single regex engine; `UseRe2Engine` is removed.

## Reusing mtsuite

- **Decided: copy the code** of mtsuite `src/core-filesystem` (no dependencies) and the traversal
  part of `src/shared` (`ParallelFileSystem`, `IDirectorCollector`, `SearchPatternParser` if useful)
  into `third_party/mtsuite/`, as a library project used by the server. The code is **re-organized as
  needed** for the server (projects, namespaces, file layout, API shape); the mtsuite layout does not
  have to be preserved. The original file headers and the Apache 2.0 `LICENSE` are kept.
  Alternatives considered: git submodule (couples the two repositories' build layouts), NuGet package
  (requires publishing mtsuite libraries first).
- **`third_party/mtsuite/PORT_CHANGES.md`** records the mtsuite commit the code was copied from
  (`c9123d8`, v0.10.0) and **every change made for this port** (re-organization, API changes, bug
  fixes, new features such as cancellation or scoped concurrency), each with its motivation, written
  so that the changes **can be backported to mtsuite** to make its file system code a reusable
  library (e.g. a `MtSuite.FileSystem` package used by both mtsuite and VsChromium). It is updated in
  the same commit as the change.
- Not reused: the mtsuite CLI layer (command line, console progress printers), the copy / mirror /
  delete / compact operations, `System.Diagnostics.PerformanceCounter` (unused in mtsuite).
- mtsuite `mtfindstr` is reused as **design** (pooled buffers, vectorized byte search, binary
  detection, UTF-8 column counting), not as a library: it is literal and case-sensitive only.
- Gaps to fill (candidates for upstreaming): cancellation, scoped concurrency, file identity
  (inode/file id) in entries if needed for incremental indexing, cached `FullPath.FullName`.

## Benchmarks

Benchmarks are **tests** (a dedicated test project, `src/Benchmarks`, run on demand, not in the
regular test pass) that **generate a real-world-like large code base on disk, then drive the server
executable** over its stdio JSON protocol, exactly as the clients do. The same benchmarks run against
any server executable (the current one on `main` for the baseline, the ported one afterwards), given
by path.

### Code base generator

- Generates, from a seed (fully deterministic, so runs are comparable), a source tree that looks like
  a real large project (Chromium-like): realistic directory structure (depth, fan-out, names such as
  `base/`, `content/browser/`, `third_party/...`, `out/` build directories), realistic file name and
  extension distribution (`.cc`, `.h`, `.mm`, `.py`, `.gn`, `.gni`, `.mojom`, `.json`, `.md`, `.html`,
  `.js`, ...), realistic file size distribution (many small files, a long tail of large ones, a few
  multi-MB files), realistic content (C++/Python/GN-like text with identifiers, comments, includes,
  license headers, long and short lines, CRLF and LF line endings), and a mix of ASCII, UTF-8
  (non-ASCII identifiers/comments, BOMs) and binary files (images, archives, `.obj`).
- Plants **known strings** at known locations (including at 100 KB chunk boundaries, in large files,
  in non-ASCII lines) so query results can be verified exactly, not only timed.
- Sizes are configurable: *small* (seconds, for development), *medium*, *large* (Chromium scale:
  ~400k files / ~5 GB). The generated corpus is cached on disk between runs (keyed by seed and size).
- An optional real checkout (e.g. a pinned Chromium revision) can be used instead, by path.

### Server statistics entry point

The server gets a new request, **`GetServerStatisticsRequest`** (added first on the current server
so the baseline has it too; breaking protocol changes are allowed), returning the server's own view
of its resource usage, measured inside the process:

- process: CPU time (user, kernel), working set, private bytes, peak working set, thread count,
  handle count (Windows), uptime;
- managed memory: GC heap size, total allocated bytes, GC counts per generation, total GC pause time,
  fragmentation (`GC.GetGCMemoryInfo`);
- native memory explicitly allocated by the server (file contents), with the number of files/bytes
  indexed;
- timings of the last operations (file system scan, file contents load, last search), measured with
  `Stopwatch` inside the server.

### Metrics collected by the benchmarks

- **Response time**: measured by the benchmark around each request (end-to-end, including JSON
  serialization and transport) **and** by the server (internal timings), so protocol overhead is
  visible.
- **CPU usage**: server process CPU time per scenario/query (from `GetServerStatisticsRequest`, cross-
  checked with `Process.TotalProcessorTime` sampled from the benchmark process).
- **Memory usage, managed and native**: sampled from outside the process every ~50 ms (working set,
  private bytes; `/proc/<pid>/status` on Linux) to get peaks, plus the server's own breakdown (GC heap
  vs native file contents vs other) from `GetServerStatisticsRequest`.

### Scenarios

1. *Cold index*: start the server, register the corpus root, wait for `SearchEngineFilesLoaded`:
   wall time, CPU time, peak memory.
2. *Steady state*: memory after indexing when idle, and after a forced GC.
3. *Queries*: a fixed query set (short/long literals, case-insensitive, whole word, simple and
   complex regex, file path patterns, low/high hit counts, hitting max results), each run N times
   after warm-up: latency p50/p95/max, server CPU time per query, response size.
4. *Extracts*: `GetFileExtractsRequest` for the result files (as the clients do when scrolling).
5. *Incremental update* (later): add/modify/delete files, time until searches reflect the change.

### Results

- **Correctness**: every query's results are checked against the planted strings (and hit counts
  compared between the two servers); only the recorded [behavior changes](#behavior-changes) are
  accepted as differences.
- Each run writes a JSON result file (machine, OS, CPU, server version, corpus seed/size, all
  metrics) and a markdown report comparing two result files (baseline vs candidate). Baseline
  results are committed under `docs/portable-server/benchmarks/`.

## Plan (milestones)

1. **Benchmark suite + baseline** on the current server: `GetServerStatisticsRequest` added to the
   current (net472) server, code base generator, benchmark tests driving the server executable over
   stdio, metrics and report; record the baseline results. Also write the regression tests for
   behavior changes 1–4 (expected to fail on the current server, marked as known failures until
   fixed).
2. **Project restructuring**: SDK-style projects, `VsChromium.Core.Ipc` (`netstandard2.0`), server
   still net472 + native code; all clients/tests green.
3. **Server on net10 (Windows)**, still with the native code (P/Invoke to VsChromium.Native.dll works
   on .NET 10): MEF/NLog/protobuf-net/Newtonsoft upgrades, new test project. Benchmark.
4. **Managed search**: text kind, line extents, literal search, regex; native code still available
   for side-by-side parity tests. Fix behavior changes 1–4 (their regression tests now pass, the
   known-failure markers are removed). Benchmark.
5. **mtsuite file system**: paths, enumeration, parallel scanning, file reading, memory accounting.
   Delete `Native`, `re2`, `ServerNativeInterop` and Win32 P/Invokes from the server. Benchmark.
6. **Process model**: VS package starts the server directly over stdio (no Host.exe); VSIX and VS
   Code packaging of the self-contained server.
7. **Linux and macOS**: path/case-sensitivity, file watching limits, CI runs of tests and benchmarks;
   VS Code VSIX for linux/macOS targets.
