// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as vscode from 'vscode';
import {
  FilesRegisterRequest,
  FilesUnregisterRequest,
  IndexCacheState,
  IndexStatisticsRequest,
  ProjectSetOptionsRequest,
  TreeGetRequest,
} from './protocol/protocol.g';
import { ServerClient, userErrorMessage } from './server/client';
import { findServerPath, serverArguments, serverPathCandidates, ServerRestartSettings } from './server/serverLocator';
import { describeIndexingState, IndexingState, nextIndexingState, subscribeIndexEvents } from './ui/indexingState';
import { CodeSearchViewProvider } from './ui/codeSearchView';
import { FileNamesPick } from './ui/fileNamesPick';
import {
  decorateIndexingDisplay,
  IndexCacheSavingPollIntervalMs,
  shouldPollIndexCache,
} from './ui/indexCacheStatus';
import { DashboardPanel } from './ui/dashboard/dashboardPanel';
import { folderForProject, isSameOrInside } from './ui/projectSettings';

/** Minimum interval between two displays of the progress of a load in the status bar. */
const ProgressDisplayIntervalMs = 1000;

/**
 * Owns the VsChromium.Server.exe process: starts it, registers the workspace folders and opened
 * files, tracks the indexing state (status bar) and restarts the server on demand.
 */
class ServerManager implements vscode.Disposable {
  private client: ServerClient | undefined;
  private state: IndexingState = { kind: 'starting' };
  /** The state of the index cache (index/statistics), requested after each load. */
  private indexCache: IndexCacheState | undefined;
  /** Next request of the state of the index cache while it is being saved. */
  private indexCachePoll: NodeJS.Timeout | undefined;
  private readonly statusBar: vscode.StatusBarItem;
  private readonly disposables: vscode.Disposable[] = [];
  /** The index cache option sent to the server for each project (by path): to confirm the deletions. */
  private readonly cacheEnabledSent = new Map<string, boolean>();
  private readonly serverChangedEmitter = new vscode.EventEmitter<void>();
  /** Fired when the server client changes (started, restarted). */
  readonly onDidChangeServer = this.serverChangedEmitter.event;

  constructor(
    private readonly context: vscode.ExtensionContext,
    private readonly output: vscode.LogOutputChannel,
  ) {
    this.statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 0);
    this.statusBar.name = 'VsChromium Indexing';
    this.disposables.push(
      this.statusBar,
      this.serverChangedEmitter,
      vscode.workspace.onDidChangeWorkspaceFolders(e => this.onWorkspaceFoldersChanged(e)),
      vscode.workspace.onDidOpenTextDocument(doc => this.registerDocument(doc)),
      vscode.workspace.onDidChangeConfiguration(e => {
        if (e.affectsConfiguration('vschromium.trigramFilters') || e.affectsConfiguration('vschromium.cacheEnabled')) {
          void this.applyProjectOptions(/*includeIndexedProjects*/ true, /*confirmCacheDeletion*/ true);
        }
        if (ServerRestartSettings.some(name => e.affectsConfiguration(name))) {
          void this.offerRestart('the index cache directory changed');
        }
      }),
    );
    this.setState({ kind: 'starting' });
    this.statusBar.show();
  }

  /** The running server client, if any. */
  get server(): ServerClient | undefined {
    return this.client && !this.client.hasExited ? this.client : undefined;
  }

  async start(): Promise<void> {
    const config = vscode.workspace.getConfiguration('vschromium');
    const configuredPath = config.get<string>('serverPath');
    const preferRepositoryBuild = this.context.extensionMode === vscode.ExtensionMode.Development;
    const serverPath = findServerPath(this.context.extensionPath, configuredPath, preferRepositoryBuild);
    if (!serverPath) {
      const candidates = serverPathCandidates(this.context.extensionPath, configuredPath, preferRepositoryBuild);
      const message = 'VsChromium.Server.exe not found';
      this.output.error(`${message}. Looked in:\n  ${candidates.join('\n  ')}`);
      this.setState({ kind: 'stopped', message });
      void vscode.window.showErrorMessage(`VsChromium: ${message}. See the VsChromium output for details.`);
      return;
    }

    const args = serverArguments({ cacheDirectory: config.get<string>('cacheDirectory') });
    this.output.info(`Starting indexing server: ${serverPath} ${args.join(' ')}`);
    this.setState({ kind: 'starting' });
    const trace = config.get<boolean>('trace.server') ?? false;
    const client = ServerClient.start(serverPath, {
      args,
      log: message => this.output.info(message),
      trace: trace ? message => this.output.trace(message) : undefined,
      clientName: 'VS Code',
      clientVersion: String(this.context.extension.packageJSON.version ?? ''),
    });
    this.client = client;
    this.cacheEnabledSent.clear();
    this.serverChangedEmitter.fire();

    subscribeIndexEvents(client, event => {
      if (this.client !== client) {
        return;
      }
      if (event.type === 'loaded') {
        // Projects discovered since the last load (e.g. from an opened file).
        void this.applyProjectOptions(/*includeIndexedProjects*/ true);
      }
      const next = nextIndexingState(this.state, event);
      if (next) {
        this.setState(next);
        if (next.kind === 'ready') {
          void this.refreshStatistics();
        }
      }
    });
    client.onExit(reason => {
      if (this.client === client) {
        this.setState({ kind: 'stopped', message: reason });
      }
    });

    try {
      const info = await client.ready();
      this.output.info(`Indexing server ${info.serverVersion} started (pid ${info.serverProcessId}).`);
      await this.registerWorkspace();
    } catch (e) {
      const detail = userErrorMessage(e) ?? String(e);
      this.output.error(`Error starting indexing server: ${detail}`);
      void vscode.window.showErrorMessage(
        `VsChromium: failed to start indexing server (${detail}).`, 'Show Output').then(choice => {
        if (choice === 'Show Output') {
          this.output.show();
        }
      });
    }
  }

  async restart(): Promise<void> {
    const old = this.client;
    this.client = undefined;
    if (old) {
      this.output.info('Stopping indexing server...');
      await old.dispose();
    }
    await this.start();
  }

  /** Settings applied when the server starts changed: offers to restart it. */
  private async offerRestart(reason: string): Promise<void> {
    const choice = await vscode.window.showInformationMessage(
      `VsChromium: ${reason}. Restart the indexing server to apply them.`, 'Restart Server');
    if (choice === 'Restart Server') {
      await this.restart();
    }
  }

  /** Shows the server dashboard, or offers to restart the server if it is not running. */
  async showStatus(dashboard: { show(): void }): Promise<void> {
    if (!this.server) {
      const choice = await vscode.window.showWarningMessage(
        'VsChromium: the indexing server is not running.', 'Restart Server', 'Show Output');
      if (choice === 'Restart Server') {
        await this.restart();
      } else if (choice === 'Show Output') {
        this.output.show();
      }
      return;
    }
    dashboard.show();
  }

  dispose(): void {
    if (this.indexCachePoll) {
      clearTimeout(this.indexCachePoll);
      this.indexCachePoll = undefined;
    }
    if (this.pendingProgressDisplay) {
      clearTimeout(this.pendingProgressDisplay);
      this.pendingProgressDisplay = undefined;
    }
    void this.client?.dispose();
    this.client = undefined;
    for (const d of this.disposables) {
      d.dispose();
    }
  }

  private async registerWorkspace() {
    const folders = vscode.workspace.workspaceFolders ?? [];
    for (const folder of folders) {
      await this.registerPath(folder.uri);
    }
    for (const doc of vscode.workspace.textDocuments) {
      await this.registerDocument(doc);
    }
    // Applied by the server when the projects load (any path inside a project identifies it).
    await this.applyProjectOptions(/*includeIndexedProjects*/ false);
    // The server sends indexing notifications only if it found a project for one of the paths.
    const stats = await this.server?.request(IndexStatisticsRequest.type, {});
    if (stats && stats.projectCount === 0 && this.state.kind === 'starting') {
      this.setState({ kind: 'noProject' });
    }
  }

  trigramFiltersEnabled(): boolean {
    return vscode.workspace.getConfiguration('vschromium').get<boolean>('trigramFilters') ?? false;
  }

  /**
   * Enables or disables the trigram filters (setting "vschromium.trigramFilters"), in the
   * workspace settings if the workspace overrides the setting, otherwise in the user settings.
   */
  async toggleTrigramFilters(): Promise<void> {
    await this.setTrigramFilters(!this.trigramFiltersEnabled());
  }

  /** Sets the setting "vschromium.trigramFilters" (see toggleTrigramFilters). */
  async setTrigramFilters(enabled: boolean): Promise<void> {
    const config = vscode.workspace.getConfiguration('vschromium');
    const inspect = config.inspect<boolean>('trigramFilters');
    const target = inspect?.workspaceFolderValue !== undefined || inspect?.workspaceValue !== undefined
      ? vscode.ConfigurationTarget.Workspace
      : vscode.ConfigurationTarget.Global;
    // Note: The configuration change listener sends the new option to the server.
    await config.update('trigramFilters', enabled, target);
    void vscode.window.showInformationMessage(enabled
      ? 'VsChromium: trigram filters enabled (built in the background; uses about 5% of the size of the indexed files in memory).'
      : 'VsChromium: trigram filters disabled (memory released).');
  }

  /**
   * The configuration of a project: the settings of the workspace folder containing it (or inside
   * it), otherwise of the workspace (resource scoped settings).
   */
  private projectConfiguration(projectPath: string): { config: vscode.WorkspaceConfiguration; folder?: vscode.WorkspaceFolder } {
    const folders = (vscode.workspace.workspaceFolders ?? []).filter(folder => folder.uri.scheme === 'file');
    const folderPath = folderForProject(projectPath, folders.map(folder => folder.uri.fsPath));
    const folder = folders.find(f => f.uri.fsPath === folderPath);
    return { config: vscode.workspace.getConfiguration('vschromium', folder?.uri ?? vscode.Uri.file(projectPath)), folder };
  }

  /**
   * Enables or disables the trigram filters of a project (server dashboard): sets the setting
   * "vschromium.trigramFilters" of the workspace folder of the project (or of the workspace).
   */
  async setProjectTrigramFilters(projectPath: string, enabled: boolean): Promise<void> {
    await this.updateProjectSetting(projectPath, 'trigramFilters', enabled);
  }

  /**
   * Enables or disables the index cache of a project: sets the setting "vschromium.cacheEnabled" of
   * the workspace folder of the project (or of the workspace). Disabling it deletes the cache of
   * the project (after a confirmation).
   */
  async setProjectCacheEnabled(projectPath: string, enabled: boolean): Promise<void> {
    await this.updateProjectSetting(projectPath, 'cacheEnabled', enabled);
  }

  private async updateProjectSetting(projectPath: string, name: string, value: boolean): Promise<void> {
    const { config, folder } = this.projectConfiguration(projectPath);
    const target = folder ? vscode.ConfigurationTarget.WorkspaceFolder
      : vscode.workspace.workspaceFolders ? vscode.ConfigurationTarget.Workspace : vscode.ConfigurationTarget.Global;
    // Note: The configuration change listener sends the new options to the server.
    await config.update(name, value, target);
  }

  /**
   * Sends the project options (settings "vschromium.trigramFilters" and "vschromium.cacheEnabled"
   * of the workspace folder of each project) for the workspace folders (any path inside a project
   * identifies the project) and, if `includeIndexedProjects`, for the projects known to the server.
   * `confirmCacheDeletion`: asks before disabling the cache of a project (its cache is deleted).
   */
  private async applyProjectOptions(includeIndexedProjects: boolean, confirmCacheDeletion = false) {
    const server = this.server;
    if (!server) {
      return;
    }
    try {
      const paths = new Set((vscode.workspace.workspaceFolders ?? [])
        .filter(folder => folder.uri.scheme === 'file')
        .map(folder => folder.uri.fsPath));
      if (includeIndexedProjects) {
        const tree = await server.request(TreeGetRequest.type, {});
        for (const project of tree.projects) {
          // The folders inside a known project identify the same project.
          for (const p of [...paths]) {
            if (isSameOrInside(p, project.rootPath)) {
              paths.delete(p);
            }
          }
          paths.add(project.rootPath);
        }
      }
      for (const projectPath of paths) {
        const { config } = this.projectConfiguration(projectPath);
        let cacheEnabled = config.get<boolean>('cacheEnabled') ?? true;
        if (!cacheEnabled && confirmCacheDeletion && this.cacheEnabledSent.get(projectPath) !== false) {
          const choice = await vscode.window.showWarningMessage(
            `VsChromium: disable the index cache of "${projectPath}"? Its cache is deleted (the next starts read all the files).`,
            { modal: true }, 'Disable and Delete');
          if (choice !== 'Disable and Delete') {
            await this.updateProjectSetting(projectPath, 'cacheEnabled', true);
            cacheEnabled = true;
          }
        }
        this.cacheEnabledSent.set(projectPath, cacheEnabled);
        await server.request(ProjectSetOptionsRequest.type, {
          projectPath,
          trigramFiltersEnabled: config.get<boolean>('trigramFilters') ?? false,
          cacheEnabled,
        });
      }
    } catch (e) {
      this.output.warn(`Error setting the project options: ${userErrorMessage(e)}`);
    }
  }

  private async onWorkspaceFoldersChanged(e: vscode.WorkspaceFoldersChangeEvent) {
    for (const folder of e.added) {
      await this.registerPath(folder.uri);
    }
    for (const folder of e.removed) {
      await this.unregisterPath(folder.uri);
    }
    if (e.added.length > 0) {
      await this.applyProjectOptions(/*includeIndexedProjects*/ false);
    }
  }

  private async registerDocument(doc: vscode.TextDocument) {
    if (doc.uri.scheme === 'file') {
      await this.registerPath(doc.uri);
    }
  }

  private async registerPath(uri: vscode.Uri) {
    if (uri.scheme !== 'file' || !this.server) {
      return;
    }
    try {
      await this.server.request(FilesRegisterRequest.type, { path: uri.fsPath });
    } catch (e) {
      this.output.warn(`Error registering "${uri.fsPath}": ${userErrorMessage(e)}`);
    }
  }

  private async unregisterPath(uri: vscode.Uri) {
    if (uri.scheme !== 'file' || !this.server) {
      return;
    }
    try {
      await this.server.request(FilesUnregisterRequest.type, { path: uri.fsPath });
    } catch (e) {
      this.output.warn(`Error unregistering "${uri.fsPath}": ${userErrorMessage(e)}`);
    }
  }

  private async refreshStatistics() {
    try {
      const stats = await this.server?.request(IndexStatisticsRequest.type, {});
      if (!stats) {
        return;
      }
      this.indexCache = stats.indexCache;
      if (this.state.kind === 'ready') {
        this.setState({ kind: 'ready', fileCount: stats.fileCount, projectCount: stats.projectCount });
      } else {
        this.setState(this.state);
      }
      // Note: No periodic requests, except while the index cache is being saved (to show the end
      // of the save).
      if (shouldPollIndexCache(this.indexCache) && !this.indexCachePoll) {
        this.indexCachePoll = setTimeout(() => {
          this.indexCachePoll = undefined;
          void this.refreshStatistics();
        }, IndexCacheSavingPollIntervalMs);
      }
    } catch (e) {
      this.output.warn(`Error getting server statistics: ${userErrorMessage(e)}`);
    }
  }

  /** Last time the status bar item displayed a loading progress (see setState). */
  private lastProgressDisplay = 0;
  private pendingProgressDisplay: NodeJS.Timeout | undefined;

  private setState(state: IndexingState) {
    this.state = state;
    // Note: The progress of a load is displayed at most once per second: each update redraws the
    // status bar item, and an open tooltip flickers. Other states are displayed immediately.
    const isProgress = state.kind === 'loading' && state.progress !== undefined;
    if (isProgress) {
      const delay = this.lastProgressDisplay + ProgressDisplayIntervalMs - Date.now();
      if (delay > 0) {
        this.pendingProgressDisplay ??= setTimeout(() => {
          this.pendingProgressDisplay = undefined;
          this.setState(this.state);
        }, delay);
        return;
      }
      this.lastProgressDisplay = Date.now();
    }
    if (this.pendingProgressDisplay && !isProgress) {
      clearTimeout(this.pendingProgressDisplay);
      this.pendingProgressDisplay = undefined;
    }
    const { text, tooltip } = decorateIndexingDisplay(describeIndexingState(state), state, this.indexCache);
    // Note: Only update the status bar item when its display changes (each update redraws it,
    // and an open tooltip flickers).
    if (this.statusBar.text !== text) {
      this.statusBar.text = text;
    }
    if (this.statusBar.tooltip !== tooltip) {
      this.statusBar.tooltip = tooltip;
    }
    const command = state.kind === 'stopped' ? 'vschromium.restartServer' : 'vschromium.showServerStatus';
    if (this.statusBar.command !== command) {
      this.statusBar.command = command;
    }
  }
}

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const output = vscode.window.createOutputChannel('VsChromium', { log: true });
  const manager = new ServerManager(context, output);
  const codeSearch = new CodeSearchViewProvider(context.extensionUri, () => manager.server, output);
  const fileNames = new FileNamesPick(() => manager.server, output);
  const dashboard = new DashboardPanel(context.extensionUri, {
    getServer: () => manager.server,
    restartServer: () => manager.restart(),
    showOutput: () => output.show(),
    trigramFiltersSetting: () => manager.trigramFiltersEnabled(),
    setTrigramFiltersSetting: enabled => manager.setTrigramFilters(enabled),
    setProjectTrigramFilters: (projectPath, enabled) => manager.setProjectTrigramFilters(projectPath, enabled),
    errorMessage: e => userErrorMessage(e) ?? String(e),
    cacheSettings: () => {
      const config = vscode.workspace.getConfiguration('vschromium');
      return { enabled: config.get<boolean>('cacheEnabled') ?? true, directory: config.get<string>('cacheDirectory') ?? '' };
    },
  });
  context.subscriptions.push(
    output,
    manager,
    dashboard,
    manager.onDidChangeServer(() => dashboard.serverChanged()),
    vscode.window.registerWebviewViewProvider(CodeSearchViewProvider.viewType, codeSearch, {
      webviewOptions: { retainContextWhenHidden: true },
    }),
    vscode.commands.registerCommand('vschromium.searchCode', () => codeSearch.show(getSearchTextFromEditor())),
    vscode.commands.registerCommand('vschromium.findFileNames', () => fileNames.show(getSelectedText())),
    vscode.commands.registerCommand('vschromium.showServerStatus', () => manager.showStatus(dashboard)),
    vscode.commands.registerCommand('vschromium.restartServer', () => manager.restart()),
    vscode.commands.registerCommand('vschromium.showOutput', () => output.show()),
    vscode.commands.registerCommand('vschromium.toggleTrigramFilters', () => manager.toggleTrigramFilters()),
  );
  await manager.start();
}

/** Returns the editor selection if it is non-empty and on a single line. */
function getSelectedText(): string | undefined {
  const editor = vscode.window.activeTextEditor;
  const selection = editor?.selection;
  if (!editor || !selection || selection.isEmpty || !selection.isSingleLine) {
    return undefined;
  }
  return editor.document.getText(selection).trim();
}

/**
 * Returns the text to search for when invoking "Search Code" from an editor: the selection if it
 * is on a single line, otherwise the word at the cursor. Returns undefined if there is no editor.
 */
function getSearchTextFromEditor(): string | undefined {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    return undefined;
  }
  const selection = editor.selection;
  if (!selection.isEmpty) {
    return selection.isSingleLine ? editor.document.getText(selection) : undefined;
  }
  const wordRange = editor.document.getWordRangeAtPosition(selection.active);
  return wordRange ? editor.document.getText(wordRange) : undefined;
}

export function deactivate(): void {
  // Everything is disposed through context.subscriptions.
}
