# Server dashboard in Visual Studio

The "VsChromium Server" tool window of the Visual Studio extension hosts the server dashboard page of the VS Code extension (`src/vscode/src/ui/dashboard`) in a WebView2 control: the same page, the same messages (`dashboardMessages.ts`), the same protocol requests.

## Architecture

| Part | Where | Notes |
|---|---|---|
| Page (HTML/CSS/JS) | `src/vscode` (`out/media/dashboard.js`, `media/dashboard.css`) + `src/VsChromium/Features/ServerDashboard/dashboard.html` | Copied into the `Dashboard` folder of the output and of the VSIX by the build |
| Tool window | `ServerDashboardToolWindow`, `ServerDashboardControl` | WebView2 (WPF), user data folder `%LOCALAPPDATA%\VsChromium\WebView2`, virtual host `https://dashboard.vschromium.example/` mapped to the `Dashboard` folder, strict CSP (scripts and styles from the virtual host only), other navigations and new windows blocked |
| Bridge | `DashboardBridge` (host side of `dashboardMessages.ts`, unit tested) | Forwards the allow-listed requests (same list as the VS Code bridge) as raw JSON-RPC (`JsonRpc.InvokeWithParameterObjectAsync<JToken>`), forwards the `index/*` notifications, sends `hostState`, `visibility` (tool window shown/hidden), `theme` |
| Host commands | `VsDashboardHost` | Confirmations in message boxes; see below |
| Theme | `DashboardTheme` (mapping, unit tested), `VsDashboardTheme` (reads the VS theme) | VS colors (`EnvironmentColors`, `CommonControlsColors`) and environment font mapped to the `--vscode-*` variables of `dashboard.css`; charts/errors use the VS Code light/dark palettes chosen from the background; contrast borders in high contrast mode; sent again on `VSColorTheme.ThemeChanged`. The page applies them through the CSSOM (allowed by the CSP) |

## Commands of the page in Visual Studio

| Command | Visual Studio |
|---|---|
| `restartServer` | `IServerClient.RestartAsync`: stops the server, starts a new one with the current options (cache directory), registers the open documents again |
| `showOutput` | Opens the log of the extension (`%LOCALAPPDATA%\VsChromium\VsChromium.log`; there is no output pane) |
| `openLogDirectory` | Opens `%LOCALAPPDATA%\VsChromium` in the File Explorer |
| `openCacheSettings` | Tools > Options > VS Chromium > Index Server |
| `openProjectCacheFolder` | Resolves the directory with `project/details` (the page never sends a path to open) |
| `clearCache`, `rebuildProject` | Confirmation (message box), then `project/clearCache` / `index/clearCache` / `project/rebuild` |
| `setTrigramFiltersSetting` | The default of the options page |
| `setProjectTrigramFilters`, `setProjectCacheEnabled` | Per project override (see below); disabling the cache of a project asks for a confirmation (its cache is deleted) |

## Settings policy

Visual Studio has no per folder settings like VS Code:

- **Defaults**: "Tools > Options > VS Chromium > Index Server": trigram filters (off), index cache (on), cache directory (empty: default; applied when the server starts, the dashboard shows "applied when the server restarts" when it differs from the running server).
- **Per project overrides** (set from the dashboard): stored per user in `%LOCALAPPDATA%\VsChromium\ProjectOptions.json`, by project root path (case-insensitive). VsChromium projects are directories, not solutions: the same project gets the same options whatever solution is open (and without solution).
- `ProjectOptionsService` sends `project/setOptions` (override, otherwise default) for the projects known to the server when it starts, after each `index/loaded` (new projects) and when the options change.

## Build

The page is built from the VS Code extension sources by the build of the VsChromium project (target `BuildDashboardPage`): `npm ci` in `src\vscode` the first time, then `npm run compile:page` when the sources of the page change. This requires Node.js on the PATH; the build fails with instructions otherwise.

WebView2: the project references the `Microsoft.Web.WebView2` package (1.0.864.35) for compilation only; at runtime the assemblies of Visual Studio are used (Visual Studio ships them with binding redirects), nothing WebView2 is included in the VSIX. The runtime is the evergreen WebView2 runtime that Visual Studio requires. If WebView2 is not available, the Code Search "server status" button shows the legacy status dialog.

## Manual test (experimental instance)

1. Build the solution (Debug). Set VsChromium as the startup project, F5 (experimental instance).
2. Open a folder or solution with a `vs-chromium-project.txt` (or open a file of such a project) and wait for the index.
3. Open the dashboard: View > Other Windows > VsChromium Server, or the server status button of the Code Search tool window. Check: the header (status, versions, uptime), the Process section (memory, charts after 10-20 s), the Projects section (one block per project), Details (collapsed).
4. Theme: Tools > Options > Environment > General > Color Theme: switch Dark / Light / Blue: the page follows immediately (background, text, buttons, checkboxes, charts palette). Windows high contrast (Left Alt + Left Shift + Print Screen): borders appear around buttons, badges, table headers.
5. Visibility: put the tool window in a tab group, switch to another tab: the refreshes stop (no `index/status` in the server log), resume when shown.
6. Actions: Pause/Resume Indexing (status changes); Restart Server (the dashboard shows "Server not running", then the new pid/uptime; Code Search still works); Show Output (opens the log); Open Log Folder; per project: Rebuild Index (confirmation, then "Indexing"), Clear Cache (confirmation, the cache state becomes "not saved yet", saved again 30 s later), cache checkbox off (confirmation, cache deleted) and on, trigram filters checkbox (memory of the filters appears), Open Folder (cache directory of the project); Clear All Caches (confirmation); the default trigram filters checkbox (also in Tools > Options); "Change…" opens the options page; change the cache directory there: the dashboard shows "applied when the server restarts", Restart Server applies it.
7. Restart Visual Studio: the per project options are kept (`%LOCALAPPDATA%\VsChromium\ProjectOptions.json`).
