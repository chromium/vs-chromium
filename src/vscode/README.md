# VsChromium for VS Code

Instant indexed code search for very large source trees (for example Chromium), powered by the
same indexing server as the [VsChromium](https://chromium.github.io/vs-chromium/) Visual Studio
extension.

> **Status:** early development. The extension starts the indexing server, registers the
> workspace folders, shows the indexing status in the status bar, and provides the **Code Search**
> panel and **Find File Names**.
>
> **Windows only** for now: the indexing server (`VsChromium.Server.exe`) is a .NET Framework
> application using Win32 APIs.

## How it works

The extension starts `VsChromium.Server.exe --stdio` and exchanges JSON messages with it, one per
line, over stdin/stdout (see `src/Server/Ipc/Json` in the repository). The server exits when VS
Code closes its stdin.

Projects are discovered by the server from the workspace folders and opened files: a folder is
indexed if it (or one of its parents) contains a `vs-chromium-project.txt` file, or is the `src`
directory of a Chromium checkout.

## Development

Prerequisites: Node.js, VS Code, and the vs-chromium solution built in `Debug` (see
`HackingOnVsChromium.txt` at the root of the repository), which produces
`Binaries\Debug\VsChromium.Server.exe`.

```
cd src\vscode
npm install
npm test          # compiles, then runs unit tests and integration tests against Binaries\Debug
```

To debug the extension, open the `src\vscode` folder in VS Code and press **F5** ("Run
Extension"). In the Extension Development Host window, open a folder containing a
`vs-chromium-project.txt` file (for example `src\Tests\TestData\chromium\src` of this repository,
or a Chromium checkout).

When running from the repository, the extension uses the server from `Binaries\Debug` (or
`Binaries\Release`). The `vschromium.serverPath` setting overrides the server location.

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
| VsChromium: Show Indexing Server Status | Project/file counts, memory usage, last index update (also available by clicking the status bar item) |
| VsChromium: Restart Indexing Server | Stops and restarts the server |
| VsChromium: Show Output | Shows the VsChromium log (enable `vschromium.trace.server` to log all messages) |
