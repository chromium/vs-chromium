# mtsuite code used by the VsChromium server: port changes

This directory tracks code originating from mtsuite (same author,
Apache License 2.0, see [LICENSE](LICENSE)), used by the VsChromium indexing server (see
[docs/portable-server/PORTING_PRINCIPLES.md](../../docs/portable-server/PORTING_PRINCIPLES.md),
"Reusing mtsuite").

This file records the origin of the code and **every change made for VsChromium**, so that the
changes can be backported to mtsuite, the goal being to turn the mtsuite file system code into a
reusable library (e.g. a `MtSuite.FileSystem` package used by both mtsuite and VsChromium). Update it
in the same commit as any change to the ported code.

## Origin

- Repository: mtsuite (local checkout `C:\src\mtsuite`)
- Commit: `c9123d8e6eefdffafd936f97206a9d352f288519` (v0.10.0, "Build Linux arm64 binaries",
  2026-11-01)

## Layout

`MtSuite.CoreFileSystem.csproj` (which contained only `PathHelpers.cs`) was folded directly into
`src/Server/Files/` and unified with `VsChromium.Core.Files.PathHelpers`.

| Source in mtsuite | Location in VsChromium | Status |
|---|---|---|
| `LICENSE` | `third_party/mtsuite/LICENSE` | unchanged |
| `src/core-filesystem/PathHelpers.cs` | `src/Server/Files/IPlatformPathHelper.cs`, `src/Server/Files/PathHelpers.cs` | unified & simplified (see below) |

## Changes

- **2026-11-02** (`src/Server/Files/IPlatformPathHelper.cs`, `src/Server/Files/PathHelpers.cs`, `src/Server.Tests/Core/TestPlatformPathHelpers.cs`):
  - Moved OS-aware path logic from `mtsuite.CoreFileSystem.PathHelpers` into `VsChromium.Server`
    (`VsChromium.Core.Files`) and retired `MtSuite.CoreFileSystem.csproj`.
  - Extracted a minimal `IPlatformPathHelper` containing only the operations used by the server,
    with three concrete implementations (`WindowsPathHelper`, `LinuxPathHelper`, `MacPathHelper`,
    where Linux and macOS share `PosixPathHelper` differing only in case sensitivity), lazily
    initialized via lock-free `_platform ?? InitializePlatform()` on `PathHelpers`.
  - Dropped unused mtsuite CLI path-normalization helpers (`NormalizeUserInputPath`, `NormalizePath`,
    `AppendTrailingSeparator`, `RemoveTrailingSeparator`, `GetPathRootPrefixInfo`) from the server
    copy. (Note when backporting `IPlatformPathHelper` to mtsuite: mtsuite's `RemoveTrailingSeparator`
    has a copy-paste bug that appends a separator instead of removing it, and `IsDirectorySeparator`
    / `GetPathRootPrefixInfo` treat `\` and `X:\` as separators/roots on Linux.)
  - Backport status: pending backport to `mtsuite`.

## Used by VsChromium

- `PathHelpers` / `IPlatformPathHelper`: OS-aware path operations and case sensitivity
  (`FileNameComparer`, `FileNameComparison`, `PathComparer`, `IsAbsolutePath`, `IsFileName`, etc.).

## Not copied (yet)

- `FullPath` (class with parent chain, low memory for large trees): considered to replace
  `VsChromium.Core.Files.FullPath` on the server, decided against after measuring (the whole managed
  heap of the server is ~85 MB on a 400k files corpus, and its file system tree already uses interned
  names with parent chains), see PORTING_PRINCIPLES.md milestone 5b.
- `IFileSystem`/`FileSystemPortable` and the Linux (`getdents64`/`statx`) and macOS
  (`getattrlistbulk`) directory enumerators: the server uses the BCL `FileSystemEnumerable`
  (`PortableFileSystem`), as mtsuite does on Windows; the OS-specific enumerators are to be copied
  when the server is benchmarked on Linux/macOS (principle 6: OS-specific code only with benchmark
  evidence).
- `ParallelFileSystem` (parallel traversal): measured against the server's own parallel scanning on
  a 400k files corpus (Windows, BCL enumeration in both): same wall time (~400 ms), CPU and
  allocations. Only its design of building the result tree during the traversal
  (`IDirectorCollector<T>`) was adopted, in `ProjectRootSnapshotBuilder` (see PORTING_PRINCIPLES.md
  milestone 5b).
