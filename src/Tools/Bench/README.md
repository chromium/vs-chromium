# vschromium-bench

A portable benchmark of the indexing server: one executable to copy to any Windows, Linux or macOS
machine. It measures the server on a standard generated corpus and shows how antivirus, file system
filter drivers, Dev Drive vs NTFS and the disk affect each phase. Results from different machines
can be compared side by side.

## Running

```
vschromium-bench                       # interactive: menus with defaults (Enter accepts)
vschromium-bench run --non-interactive --disk D: --corpus 400k --server auto --repeat 3 --cache warm
vschromium-bench inventory [--json]    # machine, disks, antivirus
vschromium-bench compare results/*.json [--output comparison.md]
vschromium-bench cleanup [--corpora] [--caches] [--servers] [--results] [--all] [--disk D:] [--yes]
vschromium-bench help                  # all the options
```

At the end of an interactive run, the bench offers to save `rerun-<name>.cmd` (Windows) or
`rerun-<name>.sh` (Linux, macOS, executable) containing the exact non-interactive command line.

## What it does

1. **Inventory** (no admin needed): OS, CPU, RAM, power plan (Windows); per disk: file system, Dev
   Drive and its trust state (Windows, `FSCTL_QUERY_PERSISTENT_VOLUME_STATE`), media and bus
   (`IOCTL_STORAGE_QUERY_PROPERTY`; Linux `/sys/block/*/queue/rotational`; macOS `diskutil`), free
   space, change journal state (`FSCTL_QUERY_USN_JOURNAL` on the root directory); antivirus products
   (Windows Security Center, not available on Windows Server), Defender mode. When running elevated,
   and only if you agree (or `--list-filters yes`): the file system filter drivers (`fltmc`) and the
   Dev Drive details (`fsutil devdrv query`).
2. **Disk and corpus**: `<disk>/vschromium-corpora/<name>` (generator version 2, see
   `src/Tools/Bench.Shared/Corpus/CodeBaseGenerator.cs`), reused when its manifest matches. The bench
   refuses a corpus when the free space is below the estimate (files and index cache) plus 10%.

   | Corpus | Files | Directories | Files on disk | + index cache |
   |---|---|---|---|---|
   | 400k | 400,000 | ~87k | 3.9 GB (measured) | ~7.6 GB total |
   | 1M | 1,000,000 | ~220k | ~9.5 GB | ~18.5 GB total |
   | 2M | 2,000,000 | ~440k | ~19 GB | ~37 GB total |

   `--symlinks yes`: the same corpus plus file and directory symbolic links (a separate directory),
   skipped with the reason when the OS doesn't allow them (Windows without Developer Mode).
3. **Server**, in this order: `--server <path>`; a repository build next to the bench or in the
   current directory (`Binaries/Release/Server`, then `Debug`); an installed VS Code extension
   (`~/.vscode/extensions/chromium.vs-chromium-*/server`); GitHub releases of `chromium/vs-chromium`
   (`--server release:latest` or `release:<tag>`): assets named
   `vschromium-server-<version>-<rid>.zip` (preferred) or `vs-chromium-<version>-<rid>.vsix`,
   downloaded to the data directory, verified by size and by SHA-256 when the release has a
   checksums asset (`SHA256SUMS`, `checksums.txt` or `<asset>.sha256`). Set `GITHUB_TOKEN` if the
   rate limit of the GitHub API is exceeded.
4. **Scenarios** (`--scenarios`, each repeated `--repeat` times: median, min, max):

   | # | Name | Server process | Measures |
   |---|---|---|---|
   | 1 | `cold` | new, empty cache | time to index, files/s, scan, file load, CPU user/kernel, memory |
   | 2 | `save` | same as 1 | save duration and bytes (server), cache size on disk |
   | 3 | `restart` | new, from the cache | time to searchable, time to verified, verification method (journal or full scan), directories listed |
   | 4 | `live` | kept | 1 / 1,000 / 10,000 modified files (`--changes`): last write to the index update with all the changes |
   | 5 | `offline` | stop, modify, restart | catch-up time, directories listed, files read from disk |
   | 6 | `search` | kept | literal, case-sensitive, regex, file path: median of 10 runs |
   | 7 | `rawfs` | none | directory listing, attribute-only open, open + read + close per file (single thread), and listing of all directories on all cores |
   | 8 | `threads` | new per count | cold index at 1, 4, 8, all threads (needs a server with `--threads`) |

   The modified files get a marker line appended, then their contents and times are restored (also
   on Ctrl+C). The index cache of the run is in `<corpora dir>/bench-cache-<corpus>` and deleted at
   the end.
5. **Warm or cold file cache.** Cold flushes the OS file cache before each measured phase: Windows
   purges the standby list (`NtSetSystemInformation`, needs admin), Linux writes `drop_caches` (root),
   macOS runs `purge` (root). Only when elevated and when you agree (or `--flush-cache yes`);
   otherwise the run is labeled "warm only".
6. **Results**: `results/<machine>-<disk>-<corpus>-<timestamp>.json` (inventory, server, options,
   every value) and a Markdown summary next to it. Results stay local.

## Server features

The bench only uses the existing protocol (`index/loaded` notifications, `server/statistics`,
`project/details` with `operations.lastVerification`, `--cache-dir`, `--no-cache`) and the
`VSCHROMIUM_TEST_CACHE_SAVE_DELAYS_MS` variable (cache saved 1 s after the index settles instead of
30 s, so that the bench doesn't wait). Newer servers can offer more; the bench detects them in the
usage text of the server (`<server> --help`) and in `server/initialize`, and uses them only when
present:

| Server feature | Used for |
|---|---|
| `--bench` | Started with it; enables the `bench/*` requests listed in `server/initialize` (`benchRequests`) |
| `--threads N` | Scenario 8 (skipped otherwise) |
| `--watcher-delay-ms N` | Reported in scenario 4 |
| `--no-journal` | Scenario 5 also measures the full-scan catch-up |
| `bench/saveCache` | Scenario 2 saves on demand (`durationMs`, `bytes`) instead of waiting for the save policy |
| `bench/phases` | Per-phase timings of the last load (all numeric fields, recorded as `phase ...` metrics) |

## Publishing

`pwsh src/Tools/Bench/publish.ps1` (or `src/Tools/Bench/publish.sh`) publishes self-contained
single-file executables to `Binaries/Publish/Bench/<rid>/` for `win-x64`, `win-arm64`, `linux-x64`,
`linux-arm64`, `osx-arm64`:

- NativeAOT for the platforms of the OS running the script (NativeAOT doesn't cross-compile to
  another OS). The app has no dependency blocking NativeAOT (source generated JSON, `LibraryImport`).
- Single-file, trimmed and compressed (with the .NET runtime) for the other platforms, or when the
  native tools of the target architecture are missing.

No .NET is needed on the target machine.
