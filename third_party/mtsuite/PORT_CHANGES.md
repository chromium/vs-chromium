# mtsuite code used by the VsChromium server: port changes

This directory contains code copied from mtsuite (same author,
Apache License 2.0, see [LICENSE](LICENSE)), used by the VsChromium indexing server (see
[docs/portable-server/PORTING_PRINCIPLES.md](../../docs/portable-server/PORTING_PRINCIPLES.md),
"Reusing mtsuite").

This file records the origin of the code and **every change made for VsChromium**, so that the
changes can be backported to mtsuite, the goal being to turn the mtsuite file system code into a
reusable library (e.g. a `MtSuite.FileSystem` package used by both mtsuite and VsChromium). Update it
in the same commit as any change to the code in this directory.

## Origin

- Repository: mtsuite (local checkout `C:\src\mtsuite`)
- Commit: `c9123d8e6eefdffafd936f97206a9d352f288519` (v0.10.0, "Build Linux arm64 binaries",
  2026-11-01)

## Layout

The mtsuite source layout is kept (`src/core-filesystem/...`), so that files can be compared with
(and changes backported to) mtsuite easily. Only the files used by VsChromium are copied; they are
compiled into a single library, `MtSuite.CoreFileSystem.csproj` (namespace `mtsuite.CoreFileSystem`
unchanged), built with the VsChromium settings (`net10.0`, strong name signed with the VsChromium
key, warnings as errors).

| File | Copied | Changes |
|---|---|---|
| `LICENSE` | 2026-11-02 | none |
| `src/core-filesystem/PathHelpers.cs` | 2026-11-02 | none |

## Changes

None yet. Each entry: date, files, description, motivation, backport status.

## Used by VsChromium

- `PathHelpers.FileNameComparer` / `FileNameComparison`: OS-dependent case sensitivity of file names
  (case-sensitive on Linux, case-insensitive on Windows and macOS), used by the server path comparers
  (`Core/Files/PathComparerRegistry.cs`, `SystemPathComparer.cs`, `#if NETCOREAPP`).
- `PathHelpers.IsPathAbsolute`: absolute paths on Linux/macOS (`Core/Files/PathHelpers.cs`).

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
- `ParallelFileSystem` (parallel traversal): the server has its own parallel scanning.

