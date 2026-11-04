# VsChromium for VS Code

Instant indexed code search for very large source trees (for example Chromium), powered by the
same indexing server as the [VsChromium](https://chromium.github.io/vs-chromium/) Visual Studio
extension.

> **Status:** early development. The extension starts the indexing server, registers the
> workspace folders, shows the indexing status in the status bar, and provides the **Code Search**
> panel and **Find File Names**.
>
> **Windows only** for now: the indexing server (`VsChromium.Server.exe`, .NET 10) is being made
> portable (see `docs/portable-server/PORTING_PRINCIPLES.md`), but is only packaged for win-x64.

## How it works

The extension starts `VsChromium.Server.exe --jsonrpc` and talks to it with JSON-RPC 2.0 over
stdin/stdout (`vscode-jsonrpc`; the protocol is defined in `protocol/*.tsp` in the repository, and
`src/protocol/protocol.g.ts` is generated from it). The server exits when VS Code closes its stdin.

Projects are discovered by the server from the workspace folders and opened files: a folder is
indexed if it (or one of its parents) contains a `vs-chromium-project.txt` file, or is the `src`
directory of a Chromium checkout.

## Development

Prerequisites: Node.js, VS Code, and the vs-chromium solution built in `Debug` (see
`HackingOnVsChromium.txt` at the root of the repository), which produces
`Binaries\Debug\Server\VsChromium.Server.exe` (framework-dependent: requires the .NET 10 runtime).

```
cd src\vscode
npm install
npm test          # compiles, then runs unit tests and integration tests against Binaries\Release
                  # (or Debug; VSCHROMIUM_SERVER_PATH overrides the server)
```

To debug the extension, open the `src\vscode` folder in VS Code and press **F5** ("Run
Extension"). In the Extension Development Host window, open a folder containing a
`vs-chromium-project.txt` file (for example `src\Tests\TestData\chromium\src` of this repository,
or a Chromium checkout).

When running from the repository, the extension uses the server from `Binaries\Release\Server`
(built by the "build" task that **F5** runs first), or from `Binaries\Debug\Server` if there is no
Release build. The Release build is preferred even when the Debug build is newer: a Debug build of
the server is about 2x slower on some searches (JIT optimizations disabled). The
`vschromium.serverPath` setting overrides the server location (e.g. to run a Debug build).

## Packaging

Prerequisite: the .NET 10 SDK. Then:

```
cd src\vscode
npm run package          # or "npm run package:debug" for a Debug server
```

This publishes the server as a self-contained, single file, ReadyToRun executable for win-x64 (no
.NET runtime needed on the user machine), copies it with its default configuration files to `bin\`
(see `scripts/copy-server.js`) and creates
`vs-chromium-win32-x64-<version>.vsix`, which can be installed with
`code --install-extension vs-chromium-win32-x64-<version>.vsix` (or *Extensions: Install from
VSIX...* in VS Code).

## Code Search

Open the **VsChromium** panel (next to Terminal/Output), or press **Ctrl+Alt+;** (*VsChromium:
Search Code*) to search for the selected text or the word under the cursor.

- Results update as you type. Options: **Aa** match case, **ab** whole word, **.\*** regular
  expression, and a file filter (e.g. `*.cc;*.h`, prefix with `-` to exclude: `-*_test.cc`).
- Results are grouped by file; the matching lines are loaded as files scroll into view.
- Click a line to preview it, double-click (or **Enter**) to open it. Keyboard: **Down** from
  the search box moves into the results, **Up/Down** navigate, **Left/Right** collapse/expand.

## Find File Names

Press **Ctrl+Alt+'** (*VsChromium: Find File Names*) and type part of a file name or path
(`foo.cc`, `base/*.h`, ...). Results come from the index and update as you type.

Paths copied from build output are understood: `base/foo.cc(19,18)`,
`../../base/foo.cc:19:18: error: ...` open the file at line 19, column 18.

## Commands

| Command | Description |
|---|---|
| VsChromium: Search Code (Ctrl+Alt+;) | Opens the Code Search panel with the selection or word under the cursor |
| VsChromium: Find File Names (Ctrl+Alt+') | Searches file names/paths in the index (prefilled with the selection) |
| VsChromium: Show Indexing Server Status | Server dashboard (editor tab): the process (memory, charts of the last 2 hours: memory, CPU, indexing status; index cache directory and total; default trigram filters; pause/resume indexing, restart the server, clear all the caches, open the logs) and each project (files, memory, cache, trigram filters, initial load and incremental update times; rebuild its index, clear or disable its cache, open its cache folder), plus collapsed details (operation timings, server, cache size chart) (also available by clicking the status bar item) |
| VsChromium: Restart Indexing Server | Stops and restarts the server |
| VsChromium: Show Output | Shows the VsChromium log (enable `vschromium.trace.server` to log all messages) |

## Settings

| Setting | Default | Description |
|---|---|---|
| `vschromium.serverPath` | (empty) | Path to `VsChromium.Server.exe`; empty: the bundled server (or the repository build during development) |
| `vschromium.cacheEnabled` | `true` | Persistent index cache of the projects (per workspace folder): the index is available a few seconds after a restart (then verified in the background). Disabling it deletes the cache of the projects of the folder, after a confirmation |
| `vschromium.cacheDirectory` | (empty) | Root directory of the index cache (absolute path), passed as `--cache-dir`; empty: `%LOCALAPPDATA%\VsChromium\Cache` |
| `vschromium.trigramFilters` | `false` | Trigram filters for the projects (per workspace folder: faster selective searches, ~5% of the text size in memory); the dashboard toggle of a project writes the setting of its folder |
| `vschromium.searchCode.maxResults` | `10000` | Maximum number of matches of Search Code |
| `vschromium.searchCode.maxExtractLength` | `120` | Maximum number of characters per line in the Search Code results |
| `vschromium.searchCode.autoSearchDelayMs` | `100` | Delay after typing before searching (Search Code, Find File Names) |
| `vschromium.searchFilePaths.maxResults` | `2000` | Maximum number of files of Find File Names |
| `vschromium.trace.server` | `false` | Log all the messages exchanged with the server to the VsChromium output |

`vschromium.cacheDirectory` is applied when the server starts: when it changes, the extension
offers to restart the server. `vschromium.trigramFilters` and `vschromium.cacheEnabled` are
resource settings: they can be set per workspace folder (`.vscode/settings.json` of the folder),
and apply to the project containing the folder (or contained in it).

`vschromium.cacheDirectory` must be on a local disk: the servers coordinate through a lock file in
each project cache, and lock files on network shares are not reliable (NFS in particular).
