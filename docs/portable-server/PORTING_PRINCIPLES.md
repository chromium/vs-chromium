# Porting the VsChromium server to portable modern .NET

This document records the goals, principles and decisions of the port of the indexing/search
server (`VsChromium.Server.exe`) from .NET Framework 4.7.2 + custom C/C++ (Win32 only) to a
portable modern .NET application (Windows, Linux, macOS). It is meant to be read before working on
the `portable-server` branch, including in a future conversation with no other context.

Status: milestones 1 (benchmarks + baseline), 2 (project restructuring), 3 (server on `net10.0`) and 4 (managed search, no more C/C++) **done**; milestone 5 (portable file system) in progress: 5a (BCL file system, no more P/Invoke) done, next 5b (mtsuite paths). Branch `portable-server` (created from `main` at `30d7bb9`).

## Goals

1. **Portable server**: the server runs on Windows, Linux and macOS (x64 and arm64), built from
   a single code base with modern .NET (see [Target framework](#target-framework-and-distribution)).
2. **No custom C/C++**: remove `src/Native` (VsChromium.Native.dll), `src/re2` (2011 RE2 snapshot,
   `third_party/re2-*`) and `src/ServerNativeInterop` (done in milestone 4). P/Invoke to **OS APIs** is allowed where it
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
| `VsChromium.Server.Tests` (new) | modern .NET | – | Server unit tests (white box) |
| `VsChromium.Tests.EndToEnd` (`src/Tests.EndToEnd`, exists) | `net10.0` | – | Black-box tests over stdio: regression tests of the behavior changes + benchmarks (see below and its `README.md`) |
| `Tests` (existing) | `net472` | – | VS-side tests only |

Code shared by the VS package and the server today (paths, pattern matching, configuration,
logging) is either moved to a `netstandard2.0` library or duplicated where the server version
diverges (e.g. mtsuite `FullPath` on the server). `src/Native`, `src/re2`,
`src/ServerNativeInterop` and `third_party/re2-*` were deleted in milestone 4; the server's Win32
P/Invokes (file system) are replaced in milestone 5.

All new/ported projects use **SDK-style** csproj (shared settings: `Build/Common.Sdk.props`: output to `Binaries\<Configuration>\`, strong name signing, warnings as errors). Done in milestone 2 for `Core.Ipc`, `Core`, `ServerNativeInterop` and `Server`; the VS-side projects (`VsChromium`, `Host`, `DkmIntegration`, `Tests`) are still legacy csproj.

### Target framework and distribution

- **Decided: target framework `net10.0`** (current LTS; the installed SDK is 10.0.x). mtsuite targets
  `net8.0`; its code is compiled as part of our projects with our target framework.
- **Decided: self-contained publish per runtime identifier** (`win-x64`, `win-arm64`,
  `linux-x64`, `linux-arm64`, `osx-arm64`), so users don't need to install a .NET runtime. The VS
  VSIX bundles `win-x64` (and `win-arm64` if we add ARM64 VS support); the VS Code VSIX is already
  platform-specific (`vsce package --target ...`).
- **Done (after milestone 5a): self-contained, single file, ReadyToRun publishing** (`dotnet publish
  src/Server/Server.csproj -c Release -r win-x64`, properties in `Server.csproj`, output in
  `Binaries\<Configuration>\ServerPublish\<rid>\`: one ~98 MB `VsChromium.Server.exe` (uncompressed,
  for startup time) + the default `Configuration\` files, kept outside the bundle
  (`ExcludeFromSingleFile`) and found with `AppContext.BaseDirectory`). The Release VSIX and the VS
  Code VSIX (`npm run package`, about 40 MB) ship this executable: no .NET runtime needed on user
  machines. Debug builds keep the framework-dependent server (`Binaries\Debug\Server\`), faster to
  build and debug. Benchmarks (`docs/portable-server/benchmarks/m5b-self-contained-r2r`, published vs
  framework-dependent): cold indexing -55% wall time on the small/medium corpora (no JIT warm-up),
  short queries 10-40% faster (regex queries stopping at max results -37 to -43%), large corpus
  unchanged. Versus the .NET Framework baseline, the remaining differences are a few ms on the small
  corpus (e.g. regex queries stopping at max results 17 ms -> 26 ms: construction of the
  non-backtracking regex).- **NativeAOT is a later goal, not a requirement of the port**: it requires replacing reflection
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
  (files are being edited/built while indexed). Done in milestone 5a (`PortableFileSystem`, which
  also enumerates directories with the BCL `FileSystemEnumerable`, as mtsuite does on Windows).
- **Change notifications**: `FileSystemWatcher` (already portable). On Linux, use **one** watcher per
  root (not 3) to limit inotify watches (`fs.inotify.max_user_watches`), with a documented polling /
  rescan fallback when the limit is hit.

### File contents and search

- **Storage** (decided in milestone 4): **ASCII files stay bytes** in memory; **files with non-ASCII
  characters (UTF-8) are decoded to UTF-16** when loaded (`FileContentsFactory`, invalid UTF-8
  bytes become U+FFFD, one character per invalid byte). This makes match positions UTF-16 offsets
  (behavior change 1) and gives Unicode-aware case-insensitive search, whole word and regex for
  free, at the cost of twice the memory for non-ASCII files only (a small fraction of source files).
  Whether the contents live in GC arrays or native memory is decided in milestone 5 by benchmarks
  (still the Win32 process heap after milestone 4); either way the total is reported.
- **Text kind detection** (ASCII / UTF-8 / binary, `Server/TextSearch/TextKindDetector.cs`): managed
  port of `Text_GetKind` (same sampling heuristic for binary detection), plus a full vectorized
  `Ascii.IsValid` check so that a file is "ASCII" only if all its bytes are ASCII.
- **Line extents** (`LineExtents.cs`): managed port of `*_GetLineExtentFromPosition` (`Span.IndexOf`).
- **Literal search** (`TextMatchers.cs`): ASCII text: vectorized `ReadOnlySpan<byte>.IndexOf`;
  case-insensitive: vectorized search of the least frequent character of the pattern (both cases)
  then `Ascii.EqualsIgnoreCase` at each candidate. UTF-16 text: `ReadOnlySpan<char>.IndexOf`, and
  `SearchValues<string>` (`OrdinalIgnoreCase`) for case-insensitive search. A non-ASCII pattern
  never matches ASCII text. No Boyer-Moore/BNDM: the vectorized searches are as fast (see the
  milestone 4 benchmarks).
- **Regex**: `System.Text.RegularExpressions.Regex` with `RegexOptions.NonBacktracking` (keeps
  RE2's linear-time guarantee for user-typed patterns; like RE2, no backreferences nor
  lookarounds), `Multiline` (`^`/`$` match at line boundaries), `CultureInvariant`. .NET regexes run
  on `char`: regex searches in ASCII files widen each chunk (at most 100 KB) to UTF-16 in a pooled
  buffer (`AsciiRegexTextSearch`). A single `Regex` instance is shared by all threads.
- **Whole word**: post-filter on the characters before/after each match: letters, digits (Unicode,
  `char.IsLetterOrDigit`) and `_` are word characters.
- **Chunks**: files larger than 100 KB are split in chunks of at most 100 KB **after a new line
  character** (`FileContents.GetChunkLength`), so that matches never span chunks (except regex
  matches spanning lines).
- **Parallelism**: keep the current partitioned PLINQ model (pieces distributed over
  `ProcessorCount` partitions). The search algorithms are stateless and shared by all threads.
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

Deliberate differences from the native server (see principle 3). Each entry is decided case by
case; add new entries here (with the reason) as they are found during the port. **Every behavior
change comes with regression tests** (server unit tests and, where relevant, end-to-end tests over
stdio in `src/Tests.EndToEnd/Regression/BehaviorChangeTests.cs`) that fail on the native server and
pass after the fix. Entries 1–4 were found while auditing the native server, the others during
milestone 4; **all are fixed since milestone 4** (managed search):

1. **Positions were byte offsets** for UTF-8 files (`AsciiFileContents` searched raw bytes), while
   the clients interpret them as character offsets: wrong match selection in files with non-ASCII
   text (VS Code `document.positionAt`). The new server reports UTF-16 offsets (non-ASCII files are
   decoded to UTF-16), for search results and file extracts.
2. **Matches spanning 100 KB file chunk boundaries were missed** (`FileDatabaseBuilder` split files
   into 100 KB pieces without overlap/newline alignment). Chunks now end after a new line
   character; only regex matches spanning multiple lines can still be missed at chunk boundaries.
3. **Non-ASCII search patterns were mangled** (`Marshal.StringToHGlobalAnsi`); case-insensitive and
   whole-word matching were ASCII-only. Non-ASCII patterns now work, case-insensitive search uses
   Unicode simple case folding (`OrdinalIgnoreCase`).
4. The non-RE2 regex path used C++ `std::regex` (ECMAScript, backtracking, on bytes); both clients
   always send `UseRe2Engine=true`. The new server has a single regex engine (.NET,
   non-backtracking, on UTF-16 text); `UseRe2Engine` is ignored (to be removed from the protocol in
   milestone 6). Patterns using backreferences or lookarounds are rejected with an error message,
   as RE2 did.
5. **Regex `^` and `$` only matched at the beginning/end of the searched text** (RE2 default mode,
   and each search restarted after the previous match, so `^foo` found at most a couple of
   matches per file chunk). They now match at line boundaries (`RegexOptions.Multiline`). Note: as
   with RE2, `$` matches before `\n`, not before `\r\n`.
6. **Whole word ignored non-ASCII letters** (a match preceded by `é` was a "whole word"), and was
   not implemented at all by the (unused) UTF-16 search. Letters and digits are now Unicode-aware
   in both ASCII and UTF-16 text.
7. **Text kind detection only sampled large files** (50 slices of 4 KB): a large file with non-ASCII
   characters outside the samples was considered ASCII (searched as bytes, so wrong positions).
   ASCII now means all bytes are ASCII (`Ascii.IsValid`, vectorized); the sampling heuristic is
   still used to detect binary files. Files with a few invalid UTF-8 bytes (e.g. Latin-1) are
   decoded as UTF-8 with one U+FFFD per invalid byte, so offsets stay correct.
8. **Regex file path searches** (`SearchFilePaths` with a regex) mangled non-ASCII paths (ANSI
   conversion); they now run on the UTF-16 path.
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

Benchmarks are **tests** (in `src/Tests.EndToEnd`, test category `Benchmark`, run on demand, not in the
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
  ~400k files / ~5 GB).
- **The generated code base is kept between test runs**: tests never delete it, so running the tests
  over and over does not delete/re-create hundreds of thousands of files. It lives outside the
  repository in a persistent directory (default: `<LocalApplicationData>/VsChromium/TestCorpora/
  <size>-<seed>-v<generator version>`, overridable with an environment variable). A manifest file
  written **last** (generator version, parameters, file count, total size) marks a complete corpus:
  if it is present and matches, the corpus is reused as is; if it is missing (interrupted
  generation) or does not match (generator or parameters changed), that corpus directory is
  re-generated. Scenarios that modify files (incremental updates) work on a small separate
  directory or restore the files they change, so the shared corpus stays pristine.
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
- **Run the benchmarks twice** (or discard the first run) after the large corpus has not been used
  for a while: the cold index scenario reads ~7 GB of files, and the first run after the OS file cache
  was evicted measures the disk instead of the server (observed: 31 s instead of 10 s of wall time,
  with the same CPU time). The query timings have a run-to-run noise of about ±10% on the large corpus,
  and the CPU times of short operations are only precise to ~16 ms on Windows.

## Plan (milestones)

1. **Benchmark suite + baseline** on the current server: `GetServerStatisticsRequest` added to the
   current (net472) server, code base generator, benchmark tests driving the server executable over
   stdio, metrics and report; record the baseline results. Also write the regression tests for
   behavior changes 1–4 (expected to fail on the current server, marked as known failures until
   fixed).
2. **Project restructuring** (done): SDK-style projects, `VsChromium.Core.Ipc` (`netstandard2.0`, protobuf-net
   2.4.9, contains `Core/Ipc/**` message types and `VsChromiumVersion`), server still net472 + native code;
   all clients/tests green. MEF-exported IPC helpers (`ProtoBufSerializer`, `IpcRequestIdFactory`) and
   `IpcStreamOverNetworkStream` (uses `Logger`) stay in `Core`.
3. **Server on net10 (Windows)** (done), still with the native code (P/Invoke to VsChromium.Native.dll works
   on .NET 10): `VsChromium.Core` split into `Core` (net472, Visual Studio side only) and `Core.Server`
   (net10.0, server-only code + the few shared files of `Core` compiled as linked files, `#if NETCOREAPP`
   for differences); NLog 6, MEF from NuGet; server output in `Binaries\<Configuration>\Server` (VSIX:
   `Server\`); new `Server.Tests` project. Benchmarks (`docs/portable-server/benchmarks/m3-net10`): queries
   equal or faster (large JSON responses 20-45% faster), cold indexing +7% wall time / +16% CPU, same memory.
4. **Managed search** (done): managed text kind detection, line extents, literal search and regex
   (`src/Server/TextSearch`), non-ASCII files decoded to UTF-16, chunks split after new lines; the
   managed code was checked against the native code with randomized parity tests, then `Native`,
   `re2`, `third_party/re2-*` and `ServerNativeInterop` were deleted (the server has no C/C++ code
   anymore). Behavior changes 1–8 fixed, with regression tests. Benchmarks
   (`docs/portable-server/benchmarks/m4-managed-search`, vs the .NET Framework baseline, large
   corpus): full scan queries at parity (±10%, the run-to-run noise), case-insensitive search -28%,
   real-world queries -25%; regex queries stopping at the max results limit +1–6 ms (construction
   and warm-up of the non-backtracking regex); cold indexing +1% wall time / +7% CPU; memory +3%
   (non-ASCII files stored as UTF-16).5. **Portable file system + mtsuite**: paths, enumeration, parallel scanning, file reading, memory
   accounting. Remove the Win32 P/Invokes from the server. Benchmark.
   - **5a (done)**: `Core.Server/Files/PortableFileSystem.cs` replaces the Win32 `IFileSystem`
     implementation on the server with BCL APIs (`FileSystemEnumerable` for directories,
     `File.OpenHandle` + `RandomAccess.Read` with `FileShare.ReadWrite | Delete` for file contents,
     `FileInfo` for attributes), and file contents blocks are allocated with `NativeMemory`
     (`HeapAllocStatic`, `#if NETCOREAPP`) instead of the Win32 process heap. The Win32 files of
     `Core` (`FileSystem.cs`, `NativeFile.cs`, the `NativeMethods` of `Win32/Files`, `Win32/Memory`,
     `Win32/Handles`...) are excluded from `Core.Server`: **the server has no P/Invoke anymore**.
     Benchmarks (`docs/portable-server/benchmarks/m5a-portable-file-system`, large corpus vs
     milestone 4): file system scan unchanged; loading the file contents ~+9% (8.4 s → 9.1 s, more
     kernel time); queries unchanged; memory unchanged. **Fixed** afterwards by reading the length and
     time stamp of each file from the open handle (`IFileContentsReader`), which saves one path lookup
     per file: loading 7.2-8.5 s, cold indexing 8.2-9.6 s, back to (or better than) milestone 4
     (`docs/portable-server/benchmarks/m5c-file-read-single-lookup`).
   - **5b (next)**: mtsuite `FullPath`/`PathHelpers` (OS-aware separators and case sensitivity) on
     the server, mtsuite Linux/macOS enumerators (needs benchmarks on Linux/macOS), copied into
     `third_party/mtsuite/` with `PORT_CHANGES.md`.6. **Process model**: VS package starts the server directly over stdio (no Host.exe); VSIX and VS
   Code packaging of the self-contained server.
7. **Linux and macOS**: path/case-sensitivity, file watching limits, CI runs of tests and benchmarks;
   VS Code VSIX for linux/macOS targets.
