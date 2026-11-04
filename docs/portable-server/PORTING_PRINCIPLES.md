# Porting the VsChromium server to portable modern .NET

This document records the goals, principles and decisions of the port of the indexing/search
server (`VsChromium.Server.exe`) from .NET Framework 4.7.2 + custom C/C++ (Win32 only) to a
portable modern .NET application (Windows, Linux, macOS). It is meant to be read before working on
the `portable-server` branch, including in a future conversation with no other context.

Status: milestones 1 (benchmarks + baseline), 2 (project restructuring), 3 (server on `net10.0`), 4 (managed search, no more C/C++), 5 (portable file system: 5a done, 5b partially) and 6 (VS package over stdio + JSON) **done**; milestone 7 (Linux/macOS) in progress, untested on these platforms. Branch `portable-server` (created from `main` at `30d7bb9`).

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
9. **Incremental updates are fast and always correct.** Changes to a few files happen constantly in
   the large code bases VsChromium is used with: the latency of an incremental update (from the
   change on disk to search results reflecting it) is a primary performance metric (benchmark
   scenarios "modify 1 file" and "create 1 directory"). And mass changes (a `git checkout`
   changing thousands of files, possibly overflowing the OS notifications) must always end with an
   index identical to one built from scratch, even when processed incrementally
   (`IncrementalUpdateTests`).

## Architecture

### Project split

| Project | Target | Used by | Content |
|---|---|---|---|
| `VsChromium.Core.Ipc`, `VsChromium.Core.Ipc.Json` | – | – | Deleted in P4 of the protocol migration (VS-side copy of the typed messages and their JSON serialization; the VS package uses the generated JSON-RPC client, `src/VsChromium/Protocol/Generated`) |
| `VsChromium.VsCore` (`src/VsCore`, formerly `Core`) | `net472` | VS package, DkmIntegration | Client-only Windows code (processes, debugger, jobs, shell, registry, VS-side file system helpers) |
| `VsChromium.Core.Server` (`src/Core.Server`) | – | – | Consolidated into `VsChromium.Server` (`src/Server`) after P5 |
| `VsChromium.Server` (`src/Server`, ported) | `net10.0` | server process | All server code: JSON-RPC transport + protocol code generated from `protocol/*.tsp` (`Protocol\Generated`), managed search, portable file system, OS-aware path helpers (`IPlatformPathHelper` / `PathHelpers`), pattern matching, configuration, logging, collections |
| `MtSuite.CoreFileSystem` | – | – | Retired: `PathHelpers.cs` folded into `src/Server/Files/` via `IPlatformPathHelper` (`WindowsPathHelper`, `LinuxPathHelper`, `MacPathHelper`), tracked in `third_party/mtsuite/PORT_CHANGES.md` |
| `VsChromium.Server.Tests` (new) | modern .NET | – | Server unit tests (white box) |
| `VsChromium.Tests.EndToEnd` (`src/Tests.EndToEnd`, exists) | `net10.0` | – | Black-box tests over stdio: regression tests of the behavior changes + benchmarks (see below and its `README.md`) |
| `Tests` (existing) | `net472` | – | VS-side tests only |

**Done (2026-11-02): the server and the VS package are fully independent code bases.** The server
projects (`src/Server`, `src/Server.Tests`) don't reference or compile any file
of the VS side (`src/Core`, `src/Tests`): the files the server
used were initially copied into `src/Core.Server` and later consolidated directly into `src/Server`, and the shared unit tests into `src/Server.Tests/Core`. The only
contract between the two is the protocol (JSON-RPC over stdio, defined in `protocol/*.tsp`), checked by the end-to-end tests
(server) and the VS and VS Code client tests. Each side evolves on its own, e.g. span-based
pattern matching on the server only. The VS package still bundles the server executable in its
VSIX (packaging only). `src/Native`, `src/re2`,
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
  non-backtracking regex).
- **Done: NativeAOT compatible, no reflection** (2026-11-02). `Core.Server` and `Server` are marked
  `IsAotCompatible` (trimming and AOT analyzers, warnings are errors: no warning). The NativeAOT
  publish (`dotnet publish src/Server/Server.csproj -c Release -r win-x64
  -p:VsChromiumPublishAot=true`, output `Binaries\Release\ServerPublishAot\win-x64\`) builds without
  warning a 9.8 MB executable which passes the end-to-end tests. **Measured (m11 code): it stays an
  option, ReadyToRun + JIT is kept for packaging.** Large corpus: cold indexing and literal searches
  unchanged (bound by the OS and by memory bandwidth), regex searches 50-110% slower (no dynamic PGO
  or tiered JIT), file path searches similar; small corpus: same cold index time (~70-130 ms),
  working set 120 → 92 MB; executable 9.8 MB instead of 98 MB.
  What remains of "reflection" is `Type.Name` (diagnostics), which is supported by NativeAOT.

### Process model and protocol

- The server is started **directly** by the client (no `VsChromium.Host.exe`, no debugger
  attachment, no job object; done in milestone 6). **It exits when its input stream is closed** (client exit or crash).
- **Decided: a single transport for both clients: stdio** (the VS package switched from TCP +
  protobuf-net to stdio + JSON in milestone 6, which removed Host.exe, the TCP listener, protobuf-net
  and the debugger/job-object machinery; validated by the benchmarks).
- **Done: JSON-RPC 2.0 protocol defined in TypeSpec** (see "Protocol definition in TypeSpec" at the
  end, and `protocol/README.md`): `protocol/*.tsp` is the single definition, the code of the server
  and of both clients is generated from it. The server only speaks this protocol (`--jsonrpc`, or
  `--stdio` as an alias) since P5; the VS Code extension uses it since P3 (`vscode-jsonrpc`,
  `src/vscode/src/server/client.ts`), the VS package since P4 (StreamJsonRpc). History: until P5, the
  protocol was a "typed messages" protocol (JSON lines, one class per message with a `$type`),
  defined three times (`src/Core.Server/Ipc`, `src/Core.Ipc`, `src/vscode/src/server/messages.ts`).

### File system

- **Paths**: mtsuite `FullPath` (immutable class, parent chain, low memory for large trees) and
  `PathHelpers` (OS-dependent case sensitivity), replacing `VsChromium.Core.Files.FullPath` /
  `RelativePath` in the server. Pattern matching (`Core/Files/PatternMatching`, the `.ignore` /
  `.include` / `.patterns` configuration files) is kept (it is managed code) and made separator-
  and case-sensitivity-aware.
- **Directory enumeration**: mtsuite `IFileSystem` / `FileSystemPortable` (BCL
  `FileSystemEnumerator` on Windows, `getdents64` + `statx` on Linux, `getattrlistbulk` on macOS),
  replacing `NtQueryDirectoryFile` (`Core/Win32/Files/NativeFile.cs`).
- **Parallel scanning**: the server keeps its own parallel traversal (one task per directory, as
  mtsuite `ParallelFileSystem.TraverseDirectoryAsync`), and builds the snapshot tree during the
  traversal like mtsuite `IDirectorCollector<T>`. Copying the mtsuite code was measured as bringing
  no gain (milestone 5b).
- **File reading**: `File.OpenHandle` + `RandomAccess.Read` with `FileShare.ReadWrite | Delete`
  (files are being edited/built while indexed). Done in milestone 5a (`PortableFileSystem`, which
  also enumerates directories with the BCL `FileSystemEnumerable`, as mtsuite does on Windows).
- **Change notifications**: `FileSystemWatcher` (already portable). On Linux, use **one** watcher per
  root (not 3) to limit inotify watches (`fs.inotify.max_user_watches`), with a documented polling /
  rescan fallback when the limit is hit.

### File contents and search

- **Storage**: **all text files are stored as UTF-8 bytes** (BOM removed, `Utf8FileContents`, with
  an `IsAscii` flag). Until m14, files with non-ASCII characters were decoded to UTF-16 when loaded
  (twice the memory for these files); see "UTF-8 storage (m14)" for the search of UTF-8 text.
  Positions sent to clients are still UTF-16 offsets of the UTF-8 decoding of the file (invalid
  UTF-8 bytes count as one U+FFFD each, behavior change 1).
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

- **Done: no dependency injection container.** MEF was first replaced with
  Microsoft.Extensions.DependencyInjection (milestone 7 groundwork), then (2026-11-02) with a hand
  written composition root, `src/Server/ServerComponents.cs`: one lazily created property per
  component (31 singletons), plain constructor calls checked by the compiler, components
  disposed in reverse creation order (as the container did), a list for the former `[ImportMany]`
  (project discovery providers; the protocol and request handler lists went away with the typed
  messages protocol in P5). `TestServerComponents` checks that all components can be created.
- Logging: NLog 6 (configured by code, works with NativeAOT), logs still under the local app data folder
  (`%LOCALAPPDATA%\VsChromium` on Windows, `$XDG_STATE_HOME`/`~/.local/state/VsChromium` on Linux,
  `~/Library/Logs/VsChromium` on macOS).
- **Done: JSON protocol with `System.Text.Json`, source generated**: the generated
  `ProtocolJsonContext` (`src/Core.Server/Protocol/Generated`) since P2. The JSON serialization of
  the former typed messages protocol (`IpcJsonContext`, `IpcJsonSerializer`, removed in P5) was
  already source generated (same JSON format as the Newtonsoft.Json implementation it replaced).
  Benchmarks (`docs/portable-server/benchmarks/m8-stj-no-reflection`, large corpus): no regression;
  queries returning many results (max results, 10,000 hits) 8-30% faster (serialization).

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
   ASCII now means all bytes are ASCII (`Ascii.IsValid`, vectorized); binary files are detected
   with the git rule since entry 14. Files with a few invalid UTF-8 bytes (e.g. Latin-1) are
   decoded as UTF-8 with one U+FFFD per invalid byte, so offsets stay correct.
8. **Regex file path searches** (`SearchFilePaths` with a regex) mangled non-ASCII paths (ANSI
   conversion); they now run on the UTF-16 path.
9. **Paths longer than MAX_PATH (260 characters) were skipped** (directory entries and file change
   events). The .NET BCL supports long paths on Windows (and there is no such limit on Linux/macOS),
   so the server now indexes them (`PortableFileSystem`, `PathHelpers.IsPathTooLong` is always false
   on the server). Fixed after milestone 7 groundwork, regression test `LongPath_FileIsIndexed`. Note:
   the clients may still be unable to open such files (e.g. older Visual Studio versions).
10. **Patterns "*text" didn't match names where "text" occurs more than once** (e.g. "*_unittest.cc"
   didn't match "a_unittest.cc_unittest.cc"): an optimization of "*" followed by a text checked that
   the *first* occurrence of the text was at the end of the name. Fixed with the span based pattern
   matching (`OpAsterisk`), regression test
   `TestFileNameMatching.AsteriskTextMatchesWhenTextOccursSeveralTimes`.
11. **Files and directories were sorted with the culture sensitive comparer** after a full scan
   (e.g. "a_b.txt" before "ab.txt"), but with the ordinal path comparer after an incremental
   update, so the order of the same tree could change. Both now use the name comparer of the
   server (`SystemPathComparer.NameComparer`: ordinal, case-insensitive on Windows and macOS),
   which is also much faster than culture sensitive comparisons. Regression test
   `TestFileSystemSnapshotBuilder.ChildrenAreSortedWithTheNameComparer`.
12. **Regex matches splitting a surrogate pair** (e.g. `.` matching the first half of a character
    outside the BMP, such as an emoji) reported half a character. With UTF-8 storage (m14), such
    matches include the whole character. Unit test
    `TestUtf8FileContents.RandomTextsMatchUtf16Search` (the reference search rounds the same way).
13. **`SearchEngineFilesLoaded` was sent twice per index update, the first time before the new index
    was active** (the file database builder raised it from its "loaded" callback, with the previous
    tree version): a client searching as soon as it was notified could get the results of the previous
    index. It is now sent once, after the activation. Regression test
    `TestFilesLoadedEvent.SearchesInFilesLoadedHandlersSeeTheNewDatabase` (it also made
    `TestSearchCode.SingleOccurrenceWorks` flaky).
14. **Binary files were detected by a home-grown heuristic** (inherited from the native
    `Text_GetKind`): 50 samples of 4 KB, binary if 10% of the bytes were neither ASCII text nor
    (loosely checked) UTF-8. NUL bytes were not special, UTF-16 files (even with a byte order mark)
    were binary, text with many control characters or Latin-1 bytes could be binary, and the whole
    file was read before deciding (e.g. multi-GB git pack files). The server now follows the well
    known tools (`TextKindDetector`): a byte order mark identifies text (UTF-8, UTF-16 LE/BE;
    UTF-16 files are converted to UTF-8, positions are UTF-16 positions of the decoded text);
    otherwise **a NUL byte in the first 8,000 bytes means binary** (git's `buffer_is_binary`, same
    prefix length); otherwise the file is text (ASCII or UTF-8, invalid bytes decoded as U+FFFD).
    The decision only depends on the first 8,000 bytes, so binary files are not read (nor
    allocated) beyond them. Differences: files with a NUL in the first 8,000 bytes are binary,
    files with control characters or invalid UTF-8 but no NUL are text, UTF-16 files with a BOM
    are text. Regression tests: `BehaviorChangeTests` (#14), `TestTextKindDetector`,
    `TestFileContentsFactory`.

## Reusing mtsuite

- **Decided: copy the code** of mtsuite `src/core-filesystem` (no dependencies) and the traversal
  part of `src/shared` (`ParallelFileSystem`, `IDirectorCollector`, `SearchPatternParser` if useful)
  into `third_party/mtsuite/`, as a library project used by the server. The code is **re-organized as
  needed** for the server (projects, namespaces, file layout, API shape); the mtsuite layout does not
  have to be preserved. The original file headers and the Apache 2.0 `LICENSE` are kept.
  Alternatives considered: git submodule (couples the two repositories' build layouts), NuGet package
  (requires publishing mtsuite libraries first). **Outcome** (milestone 5b, after measuring): only
  `PathHelpers` was copied; `FullPath`, `IFileSystem`/`FileSystemPortable` and `ParallelFileSystem`
  bring no measurable gain on Windows (the server already uses the BCL enumeration and the same
  traversal design), see `PORT_CHANGES.md` "Not copied (yet)".
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
   - **5b (partially done)**: `third_party/mtsuite/` (library `MtSuite.CoreFileSystem`, with
     `LICENSE` and `PORT_CHANGES.md`: origin commit `c9123d8`, files copied, changes) contains
     mtsuite `PathHelpers.cs`, used by the server for OS-aware path handling (`#if NETCOREAPP` in
     the shared `Core/Files` code): case-sensitive path comparisons on Linux, case-insensitive on
     Windows and macOS (`PathComparerRegistry.Default`, `SystemPathComparer`), Unix absolute paths
     (`PathHelpers.IsAbsolutePath`). **Decided not to** replace `VsChromium.Core.Files.FullPath` with mtsuite `FullPath`: measured on the large corpus (393,536 files), the whole managed heap after indexing is
     85 MB (all names, snapshots and the search index included) vs 7.2 GB of file contents, and the
     file system tree already uses interned names with parent chains (`FileSystemNameFactory`), so the
     gain would be at most ~1% of the memory, for a broad refactor. Not done either, the mtsuite Linux and
     macOS directory enumerators (to be decided with benchmarks on these platforms).
     **Parallel traversal: mtsuite design adopted, code not copied.** mtsuite
     `ParallelFileSystem.TraverseDirectoryAsync` (one `Task.Run` per directory, BCL enumeration on
     Windows) was measured against the server traversal pattern with a standalone harness on the
     large corpus (40,001 directories, 400,002 files, warm cache, 24 cores, median of 5): **397 ms vs
     419 ms**, same CPU time (~8.5 s, mostly kernel) and same allocations (~92-96 MB). Both are bound by
     the directory enumeration itself, so copying `ParallelFileSystem` (and the `IFileSystem`,
     `FileSystemEntry`, `FullPath`, object pools it depends on) brings nothing, and `FullPath` is not
     needed for performance either. The one design difference that mattered is that mtsuite builds
     the result tree during the traversal (`IDirectorCollector<T>.OnDirectoryTraversed`), whereas
     the server collected all directories in a bag, then sorted them and rebuilt the tree with
     dictionaries (~130-165 ms on the large corpus). `ProjectRootSnapshotBuilder` now builds each
     `DirectorySnapshot` in its directory task once its child tasks complete: `FileSystemScan`
     805 ms → 640-715 ms (large corpus); cold indexing is unchanged (~8.1 s, dominated by loading the
     file contents; `docs/portable-server/benchmarks/m5d-snapshot-tree-in-traversal`). Same snapshots (same sort orders and filters), all suites unchanged.
     **Relative paths of files: built on demand, per-directory cache.** The server does not store the
     relative path of files: `FileName` is a (parent directory, interned name) pair, and its relative
     path (`foo\bar\baz.cc`) is built each time it is needed: by the project filters during the scan,
     and, at every query, for each of the 400k files when a file path pattern contains a directory
     separator (file path search, the file filter of code search, regex path search with `/`).
     Patterns without a separator match the file name only. Measured on the large corpus
     (single-threaded micro benchmark of the pattern matcher over all files, median of 7):

     | Pattern | Old (walk the parent chain) | Per-directory cache | Stored per file |
     |---|---:|---:|---:|
     | `zzz\*.cc` (no match) | 52 ms | 20 ms | 12.5 ms |
     | `content\*_unittest.cc` | 44 ms | 23 ms | 13 ms |
     | `browser\**\*.h` (7,021 hits) | 42 ms | 28 ms | 17 ms |
     | Garbage per query | 77 MB | 77 MB | 12 MB |
     | Managed heap after indexing | 85 MB | 90 MB | 156 MB |

     Queries run on all cores, so the end-to-end difference is ~1-2 ms per query, below the noise
     of the benchmark (`VSCHROMIUM_BENCHMARK_RUNS` was added to run more iterations). Storing the
     relative path of each file costs +71 MB (+84% of the managed heap) and made name-only path
     searches slower (larger `FileName` struct), so it was not adopted. Adopted: each
     `RelativeDirectoryName` stores its relative path (one string per directory, +5 MB), so the
     relative path of a file is one concatenation. The remaining cost is the allocation of one
     string per file (and of the file extension in `AnyPathMatcher`): the next step is to match the
     patterns on a `ReadOnlySpan<char>` built in a reusable buffer (no allocation, no memory cost).
6. **Process model** (done): the VS package starts `Server\VsChromium.Server.exe --stdio` directly
   (`ServerProcessLauncher`) and exchanges JSON lines over its standard input/output, exactly as the
   VS Code extension does. The JSON serialization (`IpcJsonSerializer`, `IpcStreamOverJsonLines`)
   moved to a separate `Core.Ipc.Json` project (Newtonsoft.Json; the VS package uses the copy of
   Visual Studio), so that `Core.Ipc` (the message classes) has no dependency at all. Removed:
   `VsChromium.Host.exe` (and the job object code it used), the TCP listener/connection on both
   sides, the protobuf-net serializer (`ProtoBufSerializer`, `IpcStreamOverNetworkStream`), the
   server TCP mode, the `UseRe2Engine` search parameter. The server exits when its standard input is
   closed (Visual Studio exits or crashes); the package closes it on dispose and terminates the
   server if it doesn't exit within 2 s. protobuf-net was then removed entirely: its attributes are gone from the message classes, the JSON
   serializer serializes all public properties (`[JsonIgnore]` to exclude one) and accepts the public
   classes of the message namespaces as `$type` values. JSON vs protobuf: the baseline benchmarks already measured large JSON responses
   (316 KB in 9 ms on the client side, 2 ms in the server), acceptable. Packaging of the
   self-contained server: see [Target framework and distribution](#target-framework-and-distribution).
   The Release VSIX must still be tested manually in Visual Studio.7. **Linux and macOS** (in progress, **untested**: no Linux/macOS machine was available):
   - Done: the server publishes for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64` and
     `osx-arm64` from Windows (cross-compiled ReadyToRun; the runtime identifier is appended to the
     intermediate path so that app hosts of different platforms don't mix). One file system watcher
     per directory instead of 3 on Linux/macOS (inotify watches are limited by
     `fs.inotify.max_user_watches`; change events then have the `FileOrDirectory` path kind). OS
     path handling (5b). The VS Code extension declares `win32`, `linux` and `darwin`, finds
     `VsChromium.Server` (no `.exe`) outside of Windows, makes it executable if needed, and has
     packaging scripts per target (`npm run package:linux-x64`, ...). GitHub Actions workflow
     `.github/workflows/server.yml`: build + server unit tests + end-to-end tests + small benchmark on
     Windows, Linux and macOS (Linux/macOS allowed to fail for now), publish of the 5 platforms.
   - Done: polling fallback when the inotify limit is reached: `DirectoryChangeWatcher.PollingFallbackState`,
     entered on watcher errors mentioning `inotify` (watch or instance limit): watchers are stopped,
     no error is reported to the client (the index stays usable), and `RescanRequired` is fired every
     5 minutes, on which `FileSystemSnapshotManager` rescans the projects (`TestDirectoryChangeWatcherFallback`).
     Previously, the watcher kept restarting and failing every 60 s, with indexing paused.
   - To do: run the workflow (or test on Linux/macOS machines) and fix what fails (server unit tests
     using Windows paths, configuration files, path separators in the protocol...), Linux/macOS
     benchmarks.

## Performance work (after the port)

Based on a review of the server code (hot paths, data structures, algorithms), changes are made
one at a time with benchmarks (large corpus, published server), recorded here.

### Search hot paths (m9, `docs/portable-server/benchmarks/m9-search-hot-paths`)

- **Span based pattern matching** (`src/Core.Server/Files/PatternMatching`): the operators and
  `IPathMatcher` match a `ReadOnlySpan<char>`; `FileName.TestRelativePath` builds the relative path
  of a file in a stack buffer (pooled array for long paths), so matching a path pattern, the project
  filters during the scan and the searchable files filter allocate nothing per file (they allocated
  one or two strings per file per query). The extension lookup of `AnyPathMatcher` uses a span
  `HashSet` alternate lookup (no substring). Behavior change 10 fixed on the way.
- **File path search**: per-batch counters and match lists instead of a contended
  `Interlocked.Increment` per file, early stop at "max results".
- **Code search**: no allocation for pieces without match (was `FindAll().Select().ToList()` per
  piece, ~35 MB of garbage per query), searched files counted per batch on their first piece
  instead of a locked bit array updated per piece, snapshot read once per query (it could change
  during a query).
- **`ParallelBatches`** (`src/Core.Server/Threads`): both searches process their items by batches
  handed out with an atomic cursor (dynamic load balancing, all workers start immediately, batches
  spread over the whole range so that searches stopping at max results see all of it).
- **Symbolic links**: the directories contained in symbolic links are computed once per snapshot;
  with no symbolic link (the common case) the per-file check is a single test (it was a walk up the
  parent chain with a hash lookup per parent, for each file of each query).
- **Search data precomputed** (`IFileDatabaseSnapshot.PrecomputeSearchData`): the file list,
  pieces and counts of a new snapshot are computed on the indexing thread before clients are
  notified, instead of by the first search (the managed heap after indexing is ~20 MB larger as a
  result: these arrays used to be created by the first query). After a file is modified, the file
  list of the previous snapshot is reused.
- **`SlimHashTable`**: growth used the smallest of the growth policy and "+1" (so ~1.2x steps, and
  quadratic above the largest tabulated prime): it now uses the largest; enumeration no longer
  skips entries located after a removal.

Results (large corpus, vs m8): full scan code searches 10-20% faster (e.g. "no match" 107 → 93
ms, "common marker" 153 → 122 ms), file path searches scanning all the files 90-95% faster (e.g.
`zzz/*.cc` 23 → 1.7 ms, `*.zzz` 32 → 1.6 ms), other path searches 30-50% faster; cold indexing unchanged. Searches stopping at max results
(10,000 hits) take 2-4 ms more: they return a different subset of the hits (which hits are
returned when the limit is reached is arbitrary), spread over more files, so the responses are ~40%
larger (320 KB → 450 KB), and their time is dominated by the response.

### Loading and incremental updates (m10, `docs/portable-server/benchmarks/m10-incremental-updates`)

- **Loading file contents is bound by the operating system.** A standalone program opening and
  reading the 400k files of the large corpus in parallel (warm cache, same APIs as the server)
  takes 6.4–7 s, of which **opening and closing the files alone takes 5.1 s** (105 s of kernel
  CPU time: ~260 µs per file open, most likely file system filter drivers such as the antivirus).
  Twice as many threads, or skipping the time stamp query, change nothing measurable. The server
  loads the contents in ~7.1 s (`FileDatabaseUpdate`): there is nothing significant left to gain in
  the server code for a cold index (only avoiding the reads altogether, e.g. a persistent cache).
- **Benchmark scenarios for incremental updates** (`ServerBenchmarks.RunUpdatesAsync`): modify a
  file (then restore it) and create a directory with a file (then delete it), on the large corpus,
  restored afterwards. The client time (until a search sees the change) is ~2.7 s, dominated by
  the debouncing of the file change notifications; the server time is the scan + database update.
  The server records sub-timings of the database update (`FileDatabaseUpdate.Entities/Load/
  Freeze/Precompute`).
- **Incremental updates no longer build the full path of every file**: `FullPathChanges`
  checks whether a file changed with its full path built in a stack buffer
  (`FileName.TestFullPath`) and span lookups (it built two strings per file for each change, for
  the 400k files).
- **Modified files reuse the pieces of the previous snapshot**: only the pieces of the modified
  files are replaced (when they are small before and after the change), instead of recomputing the
  pieces of all the files; the file table is enumerated once (`SlimHashTable.ValuesToArray`) for
  all the per-snapshot computations; the file table of a build is sized from the actual number of
  files.
- **The garbage collection after a scan is a background collection** (it was a full blocking
  collection, pausing concurrent searches).

Results (server time per change, large corpus): modify 1 file 109 → 42 ms, create 1 directory 358
→ 235 ms (`Load` 107 → 24 ms, `Precompute` 101 → 55 ms); other scenarios unchanged.

### Smaller changes (m11, `docs/portable-server/benchmarks/m11-ipc-utf8-sort`)

- **Responses are serialized directly as UTF-8** into a reusable buffer written to the standard
  output (`IpcStreamOverJsonLines` with a `Stream`), instead of a UTF-16 string re-encoded by a
  `StreamWriter` (large responses were strings of several hundred KB on the large object heap).
- **Ordinal sort of names during the scan** (behavior change 11).
- **Files modified at once (more than 16) are read in parallel** (e.g. switching branches).

Results: neutral on the benchmarks (within noise): the server time of large responses was small
already, and culture sensitive sorting was not a significant part of the scan.

### Trigram filters (m12, `docs/portable-server/benchmarks/m12-trigram-filters`)

Literal search already runs close to memory bandwidth (7 GB in ~95 ms), so the only way to make
selective searches much faster is to scan fewer bytes. Two designs were evaluated with a
standalone analysis of the large corpus (417k pieces, 7 GB): for each query, the bytes that still
need to be scanned (pieces that may contain all the trigrams of the query, ASCII case-folded):

| | Exact inverted index | Bloom 2 bits/trigram | Bloom 4 bits | Bloom 8 bits | Bloom 12 bits |
|---|---:|---:|---:|---:|---:|
| Memory (large corpus) | 2,646 MB (37%) | 166 MB (2.4%) | 332 MB (4.8%) | 662 MB (9.5%) | 993 MB (14%) |
| "VSCB_UNIQUE_MARKER_00" | 0.00% | 0.01% | 0.00% | 0.00% | 0.00% |
| "WebContents" | 37.9% | 48.2% | 40.3% | 38.3% | 38.0% |
| "GetRenderFrameHost" | 20.9% | 38.3% | 26.3% | 22.1% | 21.0% |
| "TODO(" | 0.00% | 20.1% | 0.30% | 0.00% | 0.00% |
| " = " (regex literal) | 81.5% | 90.0% | 85.2% | 82.0% | 81.6% |

(The exact index would also need ~1 GB compressed, more complex incremental updates, and gives
almost nothing more than 4–8 bit Bloom filters.) On real code (this repository, 95 MB) the
numbers are similar: 0.13 distinct trigrams per byte (0.095 for the generated corpus), Bloom 4
bits = 6.6% of the contents, filtering within a few % of the exact index.

**Adopted: per-piece Bloom filters of trigrams** (`src/Server/TextSearch/TrigramFilters.cs`), 4
bits per distinct trigram of the piece, 3 hash functions (`VSCHROMIUM_TRIGRAM_FILTER_BITS`
overrides it, 0 disables the filters):

- Each file contents has the filters of its chunks (same chunks as the pieces,
  `FileContents.GetChunks`), in the managed heap. ASCII letters are folded to lower case, so the
  same filters serve case-sensitive and case-insensitive searches (UTF-16 files are not filtered
  for case-insensitive searches: Unicode case folding).
- A search computes the trigrams every match must contain: all the entries of a text search
  (text between wildcards), the required literal of a regular expression (`RegexLiterals`), and
  skips the pieces whose filter doesn't contain them. Bloom filters have no false negatives:
  results are unchanged (all the benchmark queries verify their exact hit counts, unit tests check
  the absence of false negatives).
- The filters are **built in the background** after a database update, on half of the
  processors, only for the file contents that don't have them yet: the end of the loading is not
  delayed (building them during the loading made the cold index 2 s longer), searches scan the
  pieces without filters until then (~3 s after a cold index of the large corpus, a few ms after a
  file is modified). The server statistics report the filters memory (`trigramFiltersBytes`).

Results (large corpus, vs filters disabled; 4 vs 8 bits per trigram):

| | Off | 4 bits (adopted) | 8 bits |
|---|---:|---:|---:|
| Filters memory | 0 | 332 MB (4.6% of contents) | 663 MB |
| Cold index | 7.8 s | 7.8 s (+3.1 s in background) | 7.8 s (+3.5 s) |
| Unique / rare / large file marker | 93–97 ms | 5.4–5.5 ms | 5.6–6.8 ms |
| No match | 97 ms | 6.5 ms | 10.5 ms |
| Common marker (0.8% of the bytes) | 120 ms | 33 ms | 31 ms |
| WebContents (38% of the bytes) | 107 ms | 51 ms | 52 ms |
| Short or common patterns (max results, `void `, ` = `) | unchanged | unchanged | unchanged |

Selective searches (the common case when looking for a symbol) become 10–18x faster; searches
for text present in most files gain little or nothing. The benchmark waits for the background
index before running the queries ("Background index" scenario).

**Per project option** (`SetProjectOptionsRequest { projectPath, trigramFiltersEnabled }`, response
`DoneResponse`): the filters can be disabled and enabled for each project at any time, since they
are most of the managed heap (large corpus, after a full GC: 112 MB without filters, ~500 MB with
them, i.e. ~4.5x, about 5% of the total memory of the server). **Disabled by default** (since
the 2.6M file tree measurement: ~890 MB of managed memory for 2.2M text files; users enable them
per project, e.g. the VS Code setting `vschromium.trigramFilters`). Disabling
releases the filters of the project in the background (followed by a non-blocking compacting GC);
enabling builds them in the background; searches are always correct (they scan the pieces without
filters). The setting is kept for the lifetime of the server process (clients send it again after
starting a server), may be sent before the project is loaded, and accepts any path inside the
project. `GetProjectDetails` reports `trigramFiltersEnabled` and `trigramFiltersBytes`. Updates of
the filters (after database updates and setting changes) are serialized: a new run cancels the
current one and starts after it. E2E tests: `src/Tests.EndToEnd/Regression/ProjectOptionsTests.cs`.

### Incremental updates: latency and correctness (m13, `docs/portable-server/benchmarks/m13-incremental-latency`)

Principle 9: incremental updates must be fast (a few files change all the time) and always
correct (a `git checkout` of thousands of files must end with the same index as a fresh one).

- **Correctness tests** (`src/Tests.EndToEnd/Regression/IncrementalUpdateTests.cs`): a generated
  tree, indexed, then a "checkout" applied on several threads as fast as possible: 40% of the
  files modified, 10% deleted, 10% moved to new directories, 10% rewritten with the same contents,
  a directory deleted, 10 new directories with 500 files. The test waits until the index reflects
  the changes (exact hit counts of per-category markers), then compares the results of several
  searches and of a file path search with a server indexing the resulting tree from scratch: they
  must be identical. Three variants: 4,000 files (~2,500 changes), 30,000 files (~20,000
  changes), and the same with 4 KB OS notification buffers (`VSCHROMIUM_WATCHER_BUFFER_SIZE`),
  which makes the notifications overflow: the server must then rescan. All pass; the 20,000
  changes are processed incrementally (scan delta + database update in ~0.5 s of server time).
- **Shorter debouncing of file change notifications**: changes are posted after 0.5 s without new
  change for file modifications (was 2 s) and 1 s for creations/deletions (was 2 s), at most 5 s /
  30 s after the first change if changes keep coming (was 10 s / 60 s, e.g. during a build), with a
  watcher polling period of 0.25 s (was 1 s). Incremental updates now take tens of milliseconds
  (m10), so the debouncing delay was most of the latency.
- **Faster recovery after a notification overflow**: the watcher restarts 2 s after the error
  (was 60 s), and the index is rescanned 2 s later (was 10 s): ~4 s plus the rescan instead of
  ~70 s, during which searches returned stale results. Changes made while the watcher is
  restarting are found by the rescan, changes made during the rescan are notified and processed
  incrementally after it.
- **64 KB OS notification buffers** (was 8–16 KB, 64 KB is the maximum recommended), so that mass
  changes rarely overflow them.

Results (large corpus, client time until a search sees the change): modify 1 file 2.6 s → 0.78 s,
create 1 directory 2.8 s → 1.45 s; the "checkout" of 20,000 files with notification overflows is
consistent ~4.5 s after the overflow (was > 70 s). Other scenarios unchanged.

### UTF-8 storage (m14, `docs/portable-server/benchmarks/m14-utf8-storage`)

Files with non-ASCII characters are now stored as UTF-8 bytes, like ASCII files (they were decoded
to UTF-16: twice the memory). Search results are unchanged except for behavior change 12:

- **Literal search** (`TextSearchFactory.CreateSearches`, `GetUtf8Search`): ASCII patterns use the
  ASCII byte matchers (case-insensitive: ASCII case folding, which is what `OrdinalIgnoreCase` does
  for ASCII patterns: it never matches non-ASCII characters such as the Kelvin sign or the dotless
  i with ASCII letters). Case-sensitive non-ASCII patterns search their UTF-8 bytes
  (`Span.IndexOf`). Case-insensitive non-ASCII patterns, and patterns containing U+FFFD (which
  matches invalid bytes), decode the chunk to UTF-16 in a pooled buffer and use the UTF-16 matcher
  (`Utf8TranscodingTextSearch`).
- **Regex**: chunks of non-ASCII files are decoded to UTF-16 (same decoding as before), match
  positions are converted back to bytes (`Utf16ToUtf8RangeMapper`). Case-sensitive regexes first
  check for the bytes of their required literal; case-insensitive ones can't (the regex engine
  matches e.g. the Kelvin sign with `k`).
- **Whole word**: the characters before/after a match are decoded (`Rune`), same result as with
  UTF-16 text (characters outside the BMP are not word characters).
- **Positions**: byte ranges of the matches are converted to UTF-16 offsets
  (`Utf8FileContents.ToUtf16Ranges`, `Encoding.UTF8.GetCharCount`), from checkpoints every 64 KB
  for large files (computed on the first match). Extracts of non-ASCII files decode the file to
  UTF-16 temporarily. Chunks never split a multi-byte character.
- **Trigram filters**: built from the UTF-8 bytes for all files (the query uses the bytes of the
  literals). Not used for case-insensitive non-ASCII literals, nor for non-ASCII files with
  case-insensitive regexes (`TrigramQuery.AppliesToNonAsciiText`).
- **Tests**: `TestUtf8FileContents` compares the results (positions, whole word, case, regex,
  trigram filters, chunks, checkpoints, extracts) with the search of the decoded UTF-16 text on
  random texts mixing ASCII, 2-4 byte characters, case folding traps and invalid bytes.

Results (large corpus, ~3% of the files have non-ASCII characters): file contents 7,579 MB →
7,316 MB (-264 MB), peak working set -3%, cold index -5%, background build of the trigram filters
-16% (bytes instead of a hash set of UTF-16 trigrams). Queries unchanged or faster (common
marker -13 to -18%). The 3-run benchmark showed two queries ~10 ms slower once; with 15 runs per
query they are at the baseline (5.8 and 5.5 ms). Updates unchanged. On source trees with more
non-ASCII text the saving is proportional to the size of the non-ASCII files (about half of their
UTF-16 size).

### Protocol definition in TypeSpec (P1 of the protocol migration)

The protocol was moved to **JSON-RPC 2.0** (LSP framing), defined once in `protocol/*.tsp` (a
subset of TypeSpec, valid for the official TypeSpec tools) and generated for the server and both
clients by our own generator, `src/Tools/ProtocolGen` (C#: lexer, parser, binder enforcing the
protocol and protobuf-compatibility rules, emitters). No Node.js is needed to build: the
`GenerateProtocol` target of `Core.Server.csproj` runs the generator before compiling, only when a
definition file or the generator changed. Rules, methods and the mapping from the former typed
messages: `protocol/README.md`.

- P1 (done): definitions of all the methods (20 requests reorganized as `group/operation`, 7
  notifications; obsolete/unused requests dropped; flat search results). The server output
  (`src/Core.Server/Protocol/Generated/Protocol.g.cs`) contains the models, the System.Text.Json source
  generation context (NativeAOT compatible), the method names, `IProtocolRequestHandler`, a
  reflection-free `ProtocolDispatcher` and `ProtocolNotificationSender`. Tests: generator
  (`src/Tools/ProtocolGen.Tests`: syntax, unsupported constructs with locations, generated code,
  checked-in file up to date), generated code (`Server.Tests/Protocol/TestProtocolDispatcher`).
- P2 (done): the server speaks the new protocol when started with **`--jsonrpc`** (JSON-RPC 2.0
  over stdin/stdout, LSP framing: `Content-Length: N\r\n\r\n` + N bytes of UTF-8 JSON); `--stdio`
  keeps the typed messages protocol until both clients have moved (a server process speaks exactly
  one protocol, `ServerProtocolMode`; `ServerComponents` only creates the components of that
  protocol). Code in `src/Server/JsonRpc`: framing (`JsonRpcMessageReader`/`JsonRpcMessageWriter`,
  pooled buffer, one write per message), `JsonRpcConnection` (read loop until the end of stdin,
  System.Text.Json parsing, `ProtocolDispatcher`, errors), `JsonRpcOutput` (responses and
  notifications, thread safe), `ProtocolRequestHandler` (the requests, ported from
  `Ipc/TypedMessageHandlers`, which stay unchanged until P5), `JsonRpcEventForwarder` (service
  events to `index/*` notifications). The progress of long operations goes through a small
  `IProgressEventSender` abstraction (typed `ProgressReportEvent` or `index/progress`).
  - Requests run concurrently on the thread pool, except **`index/pause` and `index/resume`, which
    run one at a time in arrival order** (the former `RunOnSequentialQueue`).
  - **Cancellation**: `$/cancelRequest { id }` cancels the request's `CancellationToken`; a
    cancelled request fails with `-32800` (LSP `RequestCancelled`). `search/code` and `search/paths`
    link the request's `CancellationToken` into `SearchEngine`'s `TaskCancellation` scope so
    in-flight searches abort mid-batch without returning partial results; a search cancelled by a
    newer search (implicit cancellation, as before) also fails with `-32800`.
  - **Error codes**: `-32700` parse error (and invalid framing, which ends the connection), `-32600`
    invalid request, `-32601` unknown method, `-32602` invalid parameters, `-32603` internal error
    (`data` = `ErrorInfo`: message, type name, stack trace, inner error), `-32001` expected errors
    shown to the user (`RecoverableErrorException`: invalid regex, project not found...), `-32800`
    cancelled. Details: "Transport" in `protocol/README.md`.
  - Behavior differences with the typed messages protocol: flat search results; `tree/entries*`
    return names (`DirectoryEntries`, `exists: false` instead of null) and echo the requested
    relative path; `tree/get` returns no projects when `knownVersion` is current; no "hello"
    message (`server/initialize` instead); request durations are recorded as `Request:<method>`
    in the server statistics; `tree/directoryStatistics` still returned zeros (not implemented, as
    `GetDirectoryStatisticsRequest`; removed in P5).
  - Tests: `Server.Tests/JsonRpc` (framing, connection over in-memory streams with a fake handler:
    ids, void results, all error codes, cancellation, notifications, pause/resume ordering),
    `Server.Tests/ServerProcess/TestJsonRpcServer` (the executable with `--jsonrpc`). The end-to-end
    tests and the benchmarks (`src/Tests.EndToEnd`) use the new protocol (`JsonRpcServerClient`),
    plus `Protocol/JsonRpcProtocolTests`. The benchmark result files are unchanged (same scenarios
    and query names).
- P3 (done): the VS Code extension uses the new protocol (`vscode-jsonrpc`, generated
  `src/vscode/src/protocol/protocol.g.ts`).
- P4 (done): the **Visual Studio extension** uses the new protocol (`--jsonrpc`) with StreamJsonRpc
  and the generated client (`ProtocolClient`, `IProtocolNotifications`), in
  `src/VsChromium/ServerProxy`: `ServerClient` (start, `server/initialize`, notifications as
  events, connection loss reported as before by `ProcessFatalError`), `ServerError` (error codes
  -32001/-32800/-32603 + `ErrorInfo`). `DispatchThreadServerRequest<T>` keeps the threading model
  (delay and supersede by id, request on the thread pool, callbacks on the UI thread) and cancels the
  superseded running request (`$/cancelRequest`); `RunSequentially` replaces `RunOnSequentialQueue`
  for file registrations and pause/resume. The flat search results are turned into the former tree
  (project nodes containing the files named after their relative path) by
  `SearchResultTreeBuilder`. On .NET Framework, `Process.Start` may write a UTF-8 BOM (preamble of
  `Console.InputEncoding`) to the server's stdin: the client writes an ignored header line first, and the server skips a leading BOM.
  `src/Core.Ipc` and `src/Core.Ipc.Json` are deleted (`VsChromiumVersion` moved to `src/Core`).
  Tests: `src/Tests/ServerProcess` (real server through `IServerClient`), `SearchResultTreeBuilder`,
  `ServerError`.
- P5 (done): the typed messages protocol is removed from the server. JSON-RPC is the only mode
  (`--jsonrpc`, `--stdio` accepted as an alias, the usual LSP convention); `ServerProtocolMode` and
  the legacy components are gone (`IpcServer`, `src/Server/Ipc`, the receive/send threads,
  `src/Core.Server/Ipc` with the message classes and their serialization, and their tests). The few
  legacy types still used internally moved: `RecoverableErrorException` to `Core.Server/Utility`
  (namespace `VsChromium.Core.Utility`), `SearchParams`, `FilePositionSpan` and `FileExtract` to
  `Server/Search`, `IndexingServerStatus` to `Server`, `OperationTiming` to `Server/Diagnostics`.
  `tree/directoryStatistics` (unused by the clients, always returned zeros) was removed from the
  protocol.

### Elastic indexing worker pool

Bulk background indexing (`ProjectRootSnapshotBuilder`, `FileDatabaseBuilder.LoadFileContents`,
`FileDatabaseBuilder.BuildWithChangedFiles` when updating > 16 files, and
`SearchEngine.UpdateTrigramFilters`) runs on a dedicated `IIndexingWorkerPool` (`IndexingWorkerPool`
in `src/Server/Threads`), while interactive queries (`SearchCode`, `SearchFilePaths`) stay on
`ParallelBatches.For` at normal priority so typing searches remain snappy even while a foreground
build is running.

- **Why `BelowNormal` worker threads failed on Windows**: lowering process priority or running a
  hybrid pool with `ThreadPriority.BelowNormal` workers caused > 60 s priority-inversion stalls when
  all logical cores were saturated by normal-priority work (e.g. a parallel C++/Rust build). On
  Windows NT, event wakeups (`KeSetEvent`) and the 4-second Balance Set Manager boost temporarily
  let a `BelowNormal` worker claim a directory task or file batch for one quantum (~15 ms), then
  preempt it at priority 6 for 4,000 ms while `Normal` workers sit idle waiting on
  `Task.WhenAll(childTasks)` across a 15-level directory tree or batch completion.
- **Design**: **all** worker threads in `IndexingWorkerPool` run at `ThreadPriority.Normal` so any
  worker that claims a directory or file batch finishes it in < 2 ms without priority inversion:
  - `CoreThreadCount` (`Clamp(ProcessorCount / 6, 2, 8)` — 4 on a 24-thread machine) always process
    queued work, guaranteeing forward progress under heavy CPU contention while keeping foreground
    build slowdown small.
  - `ElasticThreadCount` (`Max(0, ProcessorCount - 1 - CoreThreadCount)` — 19 on a 24-thread
    machine) expand to full machine width when CPU capacity is available and park at task/batch
    boundaries within ~80 ms when contended. Capping total workers at `ProcessorCount - 1` on
    multi-core machines leaves one logical core free when no external build is running so the
    server's own workers never starve the canary.
  - A single dedicated `ThreadPriority.BelowNormal` canary thread (`CanaryLoop`) touches no locks,
    tasks, or files; while indexing is active (0% CPU when idle), it probes scheduling latency every
    20 ms and stamps `_lastCanaryHealthyTick` whenever a cycle completes within 60 ms.
  - `FileDatabaseBuilder.ComputeFileSystemEntities` also builds flat parallel `ProjectFileData[]` +
    `FileContents[]` arrays directly instead of populating a temporary 400k-entry `SlimHashTable`.
- **Measured results** (published Release server, `large-1-400000-v1` corpus, 24 logical cores, warm
  OS file cache, mean of 2 runs; "+24T load" runs 24 `ThreadPriority.Normal` CPU burn threads):

  | Variant | Cold index (idle) | Trigrams (idle) | Cold index (+24T load) | Trigrams (+24T load) | Foreground build slowdown | Search p50 (+24T load) |
  |---|---:|---:|---:|---:|---:|---:|
  | All `Normal` (`ParallelBatches`) | 6,193 ms | 2,755 ms | 9,749 ms | 5,937 ms | 46.8% | 121 ms |
  | Process `BelowNormal` | 7,331 ms | — | > 60,000 ms | > 60,000 ms | ~7% | > 60,000 ms |
  | Hybrid `2 Normal + 22 BelowNormal` workers | 6,031 ms | 2,612 ms | > 60,000 ms | > 60,000 ms | < 1% | — |
  | `IndexingWorkerPool` (`4 Core + 19 Elastic + Canary`) | 6,345 ms | 3,190 ms | 33,310 ms | 10,773 ms | **13.1%** | **91 ms** |

### Compact file database layout

`FileDatabaseSnapshot` stores the file names and their contents in two parallel arrays: the files of
each directory are a contiguous range sorted by name (`Directories`: directory → range), so a file
is found with a lookup of its directory and a binary search (no hash table of all the files). The
pieces searched by code searches (`FilePieceList`) are one `int` per small file (its index in the
arrays) plus one entry per chunk of large files. Edits of small files replace their contents slot
(shared with the previous snapshot, as before: searches running on it see the old or the new
contents) and reuse the pieces. Directories contained in symbolic links are flagged on their
`DirectoryName` (`IsInSymLink`, computed when the name is created), instead of a set of
directories looked up per file.

Managed heap after a cold index (`large-1-400000-v1`, forced full GC): 109 MB → 82.6 MB (-24%,
~67 bytes per file); searches unchanged (within the ±10% run-to-run noise). On a 2.6M file tree
the file table (99 MB), the pieces (85 MB → ~9 MB), the flat list of file names (41 MB) and the
symbolic link set (10 MB) were measured in a heap dump before the change.

### File contents arenas

The text of files up to 512 KB is stored in 16 MB arenas (`ArenaAllocator`) shared by many files,
instead of one native block and one finalizable `SafeNativeMemoryHandle` per file. Threads allocate
with a shared atomic bump pointer. Larger files keep a dedicated block. `FileContentsMemory` is
unchanged (block, offset, length): an arena is a shared `SafeNativeMemoryHandle`, released by its
finalizer when no file contents reference it anymore, so the lifetime of the contents is the same
as before. The first 8,000 bytes of a file are read into a pooled buffer before allocating, so
binary files (not read) and UTF-16 files (read into a temporary block, only their UTF-8 conversion
is stored in an arena) leave no dead bytes in arenas. Slots of edited or deleted files are dead
bytes until their arena is released (no compaction). The dead bytes are reported by
`server/statistics` and `index/statistics` (`fileContentsArenas`: arenas, used, live bytes, and
arenas more than half dead) and in the VS Code status dialog, to decide from real workflows whether
compacting the arenas is worth it.

After a cold index (`large-1-400000-v1`, forced full GC, 2 runs each):

| | GC heap | Working set | Native (counted) | Index |
|---|---|---|---|---|
| Before | 82.5 MB | 7,308 MB | 6,977 MB | 5,830–5,865 ms |
| Arenas | 68.2 MB (-17%) | 7,146 MB (-161 MB) | 6,985 MB | 5,993–6,057 ms |

The working set drop is the per-allocation overhead of the C heap. The counted native memory
includes the 8 byte alignment of the slots and the unused end of the arenas. Searches are unchanged (within run-to-run noise). On a
2.6M file tree, the 2.18M handles were 83 MB of managed heap, all finalized after each full rebuild.

### Persistent index cache (phase 1: cached contents validated by the scan)

The contents of the indexed files are written to a cache directory after each full load, so that
the next full loads (VS Code or server restart) don't read the unchanged files (`src/Server/Cache`).

- **Location**: one directory per project root, `<cache root>/<hash of the root path>/` with a
  `root.txt` (the root path). The cache root is `%LOCALAPPDATA%\VsChromium\Cache` on Windows,
  `~/Library/Caches/VsChromium` on macOS, `$XDG_CACHE_HOME/vschromium` or `~/.cache/vschromium` on
  Linux; `--cache-dir <directory>` or the `VSCHROMIUM_CACHE_DIR` environment variable override it,
  `--no-cache` (or `VSCHROMIUM_CACHE_DIR=none`) disables the cache. The end-to-end tests always use
  a temporary cache directory.
- **Format** (`IndexCacheFile`): `arenas/NNNNNN.arena`, the text as stored in memory (UTF-8 without
  BOM, 2 trailing NUL bytes, 8 byte aligned slots, at most 16 MB), immutable once written; and
  `index.bin` (format version 2, version 3 adds the tree: see phase 2; 7-bit encoded integers: header with magic, format version, path
  case sensitivity and root path; groups of files of a directory: file count, relative path of the
  directory front-coded with the previous one, then per file the name, flags ASCII/binary, last
  write time delta, size, and for text files the arena id delta, offset delta from the end of the
  previous slot, size minus text length; arena table; trailer with magic and entry count; about 34
  bytes per file instead of 104 in version 1), written as the entries are produced to
  `index.bin.tmp` and renamed over the previous one. Binary files have an entry without contents
  (no file opened to detect them again). Files larger than 512 KB are not cached (read from disk at
  each load).
- **Validation**: during a full load, before reading a file, the cache entry with the same relative
  path is used if its last write time and size are the ones of the scan (rule of the git index, no
  file opened). The contents are `Utf8FileContents` over the arena files, memory mapped read-only
  (pages of files, not native heap memory), then pre-read in the background (one byte per page).
  The changed files path (file edits) never uses the cache. The entries are kept in memory without
  an object per entry (`IndexCacheIndex`: directory paths and names in one character buffer, an
  array of structs, an open addressing hash table of the relative paths, about 80 bytes per file)
  and released at the end of the first full load using them (the writes don't need them: the
  location of mapped contents is their view and offset).
- **Writing**: see "Save policy" below (in the background, below normal priority). Contents already
  in an arena file keep their location (mapped contents, and the native arena slots written by a
  previous write of the same session: location by block and offset, weak table per block, so they
  are written once); the others are copied to new arena files. The metadata of a file in the cache
  is the metadata of the read of its contents (last write time and size from the open handle, kept
  on the file contents: 8 bytes per file), not of the scan: edited files are re-read but the scan
  is not updated. The contents of files modified less than 2 s before they were read ("racy clean"
  problem of git, flag set when the file is read) are not written. Arena files no longer referenced
  are deleted after the index is replaced (or at the next write if still mapped, or when the cache
  is opened next time).
- **Errors**: any error reading the cache (truncated, corrupt, other format version, missing or
  truncated arena file) is logged, the cache is deleted and the files are read from disk.
- **Concurrency**: the server holding the `lock` file (opened exclusively for its lifetime) writes
  the cache; the other servers on the same project use it read-only.
- **Known limitation (Windows)**: the scan has the metadata of the directory entries. NTFS doesn't
  update the directory entries of the other names of a hard link modified through one of its
  names (nor the entry of a file being written until it is closed), and the entry of a file
  symbolic link has the metadata of the link, not of its target. A file cached while its directory
  entry was up to date and later modified through another name (or whose symbolic link target
  changed) is not detected; "Rebuild index" reads all the files. Files whose scan time is not the
  time of their contents when the cache is written are not cached (`lastWriteChangedFileCount`,
  and a log line with up to 10 samples: scan and contents metadata, current metadata, attributes,
  link target), so they are read from disk at each load.
- **Rebuild index** (`tree/refresh`): the cache of the registered projects is deleted first (and the
  contents loaded from it are not reused), so that all the files are read from disk (e.g. files
  modified without changing their time and size).
- **Statistics**: `fileContentsArenas.cachedFileBytes` (mapped contents, not counted as large files
  nor as native memory), and `server/statistics` `indexCache` (files of the last full load from the
  cache and from disk, last write).

`large-1-400000-v1` (393,536 files, 3.4 GB in arenas + 3.6 GB of large files), Debug server,
cache on C: (NTFS), corpus on D: (ReFS):

| | Index (time to `index/loaded`) | Native (counted) | Private bytes | GC heap at `index/loaded` | GC heap after the write |
|---|---|---|---|---|---|
| No cache | 6,755 ms | 6,985 MB | 7,163 MB | 75.4 MB | 85.5 MB (no write) |
| Cold load (writes the cache) | 6,636–7,130 ms | 6,985 MB | 7,180 MB | 92.5 MB | 90.2 MB |
| Restart, format version 1 | 1,645–1,812 ms | 3,593 MB (large files only) | 3,880–3,895 MB | 163.1 MB | 84.6–85.7 MB |
| Restart, format version 2 | 1,530–1,658 ms (-77%) | 3,593 MB (large files only) | 3,824–3,835 MB | 76.4 MB | 84.7–85.1 MB |

The restart reads 400 files from disk (the files larger than 512 KB) and maps 378,911 files
(3,386 MB) from 212 arena files. The cache write after the cold load takes 6.5–11.3 s in the
background (3.4 GB); after a restart it only rewrites the index (12.2 MB instead of 39.4 MB in
version 1, ~0.4 s instead of ~0.7 s). Cache size on disk: 3.56 GB. The GC heap grows by ~10 MB
after `index/loaded` without the cache too (the searches of the benchmark), so the cache adds
nothing to the steady state managed heap. Searches are unchanged (within run-to-run noise).

### Persistent index cache (phase 2: cached tree, verified in the background)

`index.bin` (format version 3) also contains the tree of the project (all the directories in depth
first order with their symbolic link flag, all the files with their last write time and size,
including the files without cached contents: larger than 512 KB, not searchable, modified during
the load) and the version hash of the project configuration. When a project is registered and its
cache has a tree written with the same version hash:

1. The snapshot of the project is built from the tree of the cache (no scan), and its index from
   the cached contents (no disk access except the cache files): published immediately (publish
   delay 0, `FileSystemSnapshotManager.CachedTreePublishDelay`, hard-coded), "index/loaded" with
   `fromCache: true`, status `verifying` (VS Code status bar "VsChromium: verifying", Visual Studio
   status text "Verifying"). The files without cached contents (e.g. larger than 512 KB) are in the
   tree, without contents: missing from code searches until the verification.
2. In the background (`VerifyCachedTrees`, after the load of the cached index on the same task
   queue): the directory watcher already runs (changes are queued as during any scan), the project
   is scanned, the scan is compared with the cached tree (`SnapshotDiff`): files with another time
   or size and files without cached contents are changed, new files and directories created,
   missing ones deleted. The scanned tree is published with these synthetic changes, applied by
   the incremental update of the index (`CreateWithFileSystemChanges`, the tested path of the
   incremental updates): the index is then the same as the index of a cold load. "index/loaded"
   without `fromCache`, status `idle`, and the cache is written (tree and contents).
3. If the version hash differs (configuration or ignore rules changed), or the cache has no tree,
   the project is scanned first and the cached contents of the unchanged files are used (phase 1).

Interactions: searches and file extracts use the cached index during the verification. A project
registered during the verification cancels it (it runs again after the scan of the new project).
"Rebuild index" cancels it and deletes the cache (full rescan, all the files read from disk).
Pausing cancels it (resuming rescans everything). A second server on the same project publishes
from the cache too (read-only, no write). Note: the tasks of the task queues don't reference the
previous snapshot (only its version): the last task of a queue can stay referenced after it ran
(e.g. by an idle thread of the custom thread pool), which kept the cached tree alive after its
verification (+37 MB of managed heap on `large-1-400000-v1`).

`large-1-400000-v1`, Debug server (same machine and settings as above; "idle": status `idle`,
including the forced GC of the "at index/loaded" measurement):

| | First `index/loaded` | Idle (up to date) | GC heap at `index/loaded` | GC heap after the write |
|---|---|---|---|---|
| Cold load | 6,346–6,501 ms | 6,820–7,060 ms | 92.7 MB | 90.3–92.9 MB |
| Restart, phase 1 | 1,437–1,454 ms | 1,766–1,797 ms | 76.4 MB | 82.7–85.6 MB |
| Restart, phase 2 | 760–784 ms (`fromCache`) | 2,231–2,272 ms | 85.1–95.5 MB (verification running) | 85.3–85.7 MB |

The verification (scan, comparison, incremental update of 400 large files) takes ~1.5 s on this
corpus, whose scan is fast (D:, ReFS, 393k files): on a 2.65M entry tree, the scan (15–17 s) is
most of the time to first search of phase 1, and phase 2 publishes the cached index before it.

### Persistent index cache: save policy

The cache is saved when the index changed since the last save (any update: full load, tree
changes, edits of existing files), once the changes settle: 30 s without index update, but at most
5 minutes after the first unsaved change (continuous changes, e.g. a build, still save every
5 minutes instead of rewriting the index after each batch of changes). `IndexCacheSaveScheduler`;
the delays are constants (`IndexCacheOptions.DefaultSaveSettleDelay`/`DefaultSaveMaxDelay`, test
override `VSCHROMIUM_TEST_CACHE_SAVE_DELAYS_MS=settle,max`).

- **First save**: right after a full load of a project whose cache has no index yet (first load of
  a project, after "Rebuild index"), so that the cache exists soon after the first cold load.
- **Saves are consistent snapshots**: a save writes the state of the last update when it starts.
  Updates during a save make the index dirty again (saved later, same policy): a save is not
  cancelled by new changes (it would never complete during continuous changes). A new full load
  cancels a running save (the index stays dirty, saved after the load).
- **No save at shutdown** (closing VS Code or Visual Studio is not delayed): a running save is
  cancelled and not waited for (background thread). The previous index stays valid (written to a
  temporary file, then renamed; orphan arena files are deleted when the cache is opened). The
  changes since the last save are re-read by the verification of the tree at the next start; if the
  client is closed before the first save after a cold load, the next start is cold again.
- **Edited files** are saved with the metadata of their read (time and size from the open handle,
  on the file contents: 8 bytes per file, +2.6 MB of GC heap at `index/loaded` on
  `large-1-400000-v1`: 92.6–92.9 MB -> 95.4–95.5 MB, +0.8 MB after the save): the next start loads
  them from the cache (the verification finds the same time and size in the scan).
- **Status**: `index/statistics` `indexCache.dirty` (changed since the last save started),
  `saving` (a save is running).

### Persistent index cache: compaction and cleanup (phase 3)

The arena files are never modified: the contents of new and edited files are appended to new arena
files, and the slots of modified or deleted files stay in their arena file (dead bytes) until it is
compacted.

- **Dead bytes**: `IndexCacheArenaUsage`, computed when the index is read (from its entries) and at
  the end of each write (from the written entries): bytes used by the arena files of the index,
  bytes of the slots referenced by its entries (live), and the rest (dead). No walk at request
  time: `index/statistics` `indexCache.arenaFileCount`, `arenaBytes`, `deadBytes` (summed over the
  open projects); the VS Code status shows "(N MB dead)".
- **Compaction, during a save**: a first pass over the contents of the snapshot to save gives the
  live bytes of each existing arena file (from the location of the contents in memory, no I/O), so
  that the save which follows a churn compacts it. Compacted: the arena files with less than 50%
  live bytes, and, when more than 25% of the arena bytes are dead, the sparsest ones until less
  than 10% are dead (hysteresis: the next saves don't compact a few files each). At most 1 GB of
  live bytes copied per save (`ProjectIndexCache.DefaultCompactionMaxBytes`, test override
  `VSCHROMIUM_TEST_CACHE_COMPACTION_MAX_MB`, 0: no compaction): a compaction stopped at the limit
  is resumed by the next save (until less than 10% are dead).
  - The live slots are copied from the contents in memory (the mapped view of the old arena file,
    or the native memory of contents read from disk) to new arena files, like new contents: the
    contents in memory are not changed (searches keep using the old mapped views), only their
    location (in the cache) is updated. The next load maps the new arena files.
  - The old arena files are deleted after the index is replaced; on Windows, the deletion of an
    arena file still mapped by this server fails: retried by the next saves, or deleted when the
    cache is opened next time.
  - A compaction is part of the save: cancelled with it (new full load, shutdown: no save at
    shutdown), the previous index stays valid (the new arena files are orphans, deleted when the
    cache is opened).
- **Cleanup of old caches** (`IndexCacheCleanup`): 2 minutes after the first project cache is
  opened by a server, in the background (below normal priority), the caches of projects not used
  for 30 days are deleted, then the least recently used ones while the total size of the cache
  directory is above 50 GB (fixed limit). Never deleted: the caches open by this server, and the
  caches locked by another server (the lock file is taken exclusively during the deletion: no
  server can open the cache meanwhile). The last use of a cache is the last write time of its
  index (each save) or of its `root.txt` (each open to write): no additional bookkeeping.

Churn benchmark (Debug build, `large-1-400000-v1` copy, 379,311 text files): cold load and first
save, then 20% of the files rewritten (79,920) and 10% deleted (39,960) while the server runs, a
save, another change (one new file) and a save, then a restart. Save times vary a lot on this
machine (disk flushes, other processes): the first save after the churn took 6.7–31.7 s with
compaction across 6 runs, 2.5–13.3 s without; the table shows one pair of runs.

| | Compaction (default) | No compaction |
|---|---|---|
| Cold save | 12.4 s, 3,399 MB, 212 arena files | 12.3 s, 3,399 MB |
| First save after the churn | 12.8 s, 1,708 MB written (93 arena files compacted, 1,018 MB copied: the limit) | 2.7 s, 691 MB written |
| Dead bytes after it | 549 MB of 3,598 MB (15%) | 1,015 MB of 4,065 MB (25%) |
| Second save | 2.4 s, 505 MB written (resumed: 44 arena files compacted, 493 MB copied) | 0.3 s, 12 MB written |
| Dead bytes after it | 339 MB of 3,388 MB (10%) | 1,015 MB of 4,065 MB (25%, stays) |
| Disk size | 3,400 MB | 4,077 MB |
| Restart: first `index/loaded` / idle | 1.23 s / 3.8 s (341,042 files from the cache) | 1.22 s / 2.7 s |

The idle time of a restart (verification of the tree in the background) varied between 2.7 and
5.6 s across runs, with or without compaction.

### Per-project memory (dashboard v2)

The file contents of each project are allocated in arenas of their own (`ProjectArenaAllocators`:
one `ArenaAllocator` per project root, created on first use). `FileContentsFactory` gets the project
root for every read (full loads and reads of edited files), so that:

- The memory of each project is exact: `project/details` `memory` (native bytes, arenas, live, dead
  and free bytes, large files, contents mapped from the cache of the project, trigram filters), and
  the process-wide `fileContentsArenas` of `index/statistics` is the sum over the projects.
- The arenas of a project are released once the project is removed and its contents are not used
  anymore (old snapshots, running searches): the allocator of a removed project is forgotten when
  the first complete index without the project is activated. With a shared allocator, the slots of
  a removed project were dead bytes of arenas shared with the other projects.
- The files of a project are found from the ranges of its directories in the database (one walk of
  the parents per directory, not per file), then the slots of their contents are matched with the
  arenas of the project (contents in dedicated blocks are large files, contents in mapped views come
  from the cache of the project).

The cost is the unused end of the current arena of each project (less than 16 MB per project) and
a dictionary lookup per file read. `project/details` also returns the state of the cache of the
project (`cache`: directory, size, dead bytes, last save, dirty, saving), from the state in memory:
a project is dirty if an update touched it since the last save of its cache (updates record the
projects whose files changed).

On `large-1-400000-v1` (one project, Debug build, 2 runs each, forced full GC): the same memory
(GC heap 96.3–96.4 MB after a cold load, 78.1–78.4 MB after a restart; native 6,985 MB cold, 3,593
MB after a restart: 212 arenas, 3,386 MB live), cold load 6,569–7,037 ms before and 6,917–6,921 ms
after, first `index/loaded` of a restart 837–1,122 ms before and 829–896 ms after (run-to-run noise).

Per-project operation statistics (`project/details` `operations`, `ProjectOperations`): fixed size
accumulators per project (count, min, average, max, last; no allocation per operation).

- **Initial load** (first load of a project, rebuilds, full rescans): from the start of the scan of
  the project to its complete index. The scan time of each project is exact (projects are scanned
  one after the other); the time of the load of the files is shared by the projects loaded together
  in proportion of their files with contents (the files read, for a cold load). A load from the
  index cache records the time to the published index of the cache (`cachedIndexMs`), and its total
  is the time to the verified index (`scanMs`: the verification scan).
- **Incremental updates** (file system changes, edited files): each update counts for each project
  it touches, with the duration of the whole update.

Per-project actions:

- `project/rebuild { projectPath }`: deletes the cache of the project, rescans only this project
  (the other projects keep their tree: `fullScanRoots`), and publishes the new tree with the root
  of the project as a created directory: the update reads all the files of the project again, the
  files of the other projects are reused. Only the caches of the projects changed since their last
  save are written (the dirty projects): the caches of the other projects are not rewritten.
- `project/clearCache { projectPath }`: deletes the cache of the project without rescan (the
  current index is saved again by the save policy). `index/clearCache` (no parameter) clears all.
- `project/setOptions { cacheEnabled }`: disabled, the cache of the project is closed and its
  directory deleted (the arena files still mapped by the current index are deleted at the next
  start, when the option is sent again), and it is neither read nor written anymore; enabled
  again, the index of the project is saved by the save policy. VS Code: `vschromium.cacheEnabled`
  and `vschromium.trigramFilters` are resource scoped settings (per workspace folder: the folder
  containing the project root, or inside it), sent for each project at start and registration;
  disabling the cache asks for a confirmation. The cache root directory stays global
  (`vschromium.cacheDirectory`).

Readers of a cache written by another server (read-only): the writer writes the new arena files,
then the new index (`index.bin.tmp` renamed over `index.bin`), then deletes the arena files the new
index doesn't reference. A reader that read the old index can find one of its arena files deleted
(or replaced) before it maps it: it releases the views mapped so far, reads the (new) index again
and maps its arena files, at most 3 attempts (50 ms, 100 ms between them); after that the cache is
ignored for this load (a read-only cache is never deleted). Errors of the index itself (invalid
contents) are not retried. Readers open `index.bin` and the arena files with `FileShare.Read |
FileShare.Delete` (the writer can rename and delete them while they are open), and keep no handle
after mapping (the views stay mapped); the writer's deletion of an arena file still mapped by
another process fails on Windows and is retried by its next saves (or when it opens the cache).

The writer of a project cache is the server holding its `lock` file: opened with
`FileShare.None` for the lifetime of the cache (on Linux/macOS, .NET implements it with an advisory
`flock`). The lock is the open handle, not the existence of the file: the file stays on disk, and
the operating system releases the lock when the process exits, including when it is killed (no
stale lock to clean up). This relies on the lock semantics of a local file system: a cache
directory on a network share (`vschromium.cacheDirectory`) depends on the file server (SMB
generally honors it, NFS `flock` is not reliable), which is not supported.
