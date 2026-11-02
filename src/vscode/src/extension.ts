// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as vscode from 'vscode';
import { ServerClient } from './server/client';
import { findServerPath, serverPathCandidates } from './server/serverLocator';
import { describeIndexingState, IndexingState, nextIndexingState } from './ui/indexingState';
import { CodeSearchViewProvider } from './ui/codeSearchView';
import { FileNamesPick } from './ui/fileNamesPick';

/**
 * Owns the VsChromium.Server.exe process: starts it, registers the workspace folders and opened
 * files, tracks the indexing state (status bar) and restarts the server on demand.
 */
class ServerManager implements vscode.Disposable {
  private client: ServerClient | undefined;
  private state: IndexingState = { kind: 'starting' };
  private readonly statusBar: vscode.StatusBarItem;
  private readonly disposables: vscode.Disposable[] = [];

  constructor(
    private readonly context: vscode.ExtensionContext,
    private readonly output: vscode.LogOutputChannel,
  ) {
    this.statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 0);
    this.statusBar.name = 'VsChromium Indexing';
    this.disposables.push(
      this.statusBar,
      vscode.workspace.onDidChangeWorkspaceFolders(e => this.onWorkspaceFoldersChanged(e)),
      vscode.workspace.onDidOpenTextDocument(doc => this.registerDocument(doc)),
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
    const serverPath = findServerPath(this.context.extensionPath, configuredPath);
    if (!serverPath) {
      const candidates = serverPathCandidates(this.context.extensionPath, configuredPath);
      const message = 'VsChromium.Server.exe not found';
      this.output.error(`${message}. Looked in:\n  ${candidates.join('\n  ')}`);
      this.setState({ kind: 'stopped', message });
      void vscode.window.showErrorMessage(`VsChromium: ${message}. See the VsChromium output for details.`);
      return;
    }

    this.output.info(`Starting indexing server: ${serverPath}`);
    this.setState({ kind: 'starting' });
    const trace = config.get<boolean>('trace.server') ?? false;
    const client = new ServerClient(serverPath, {
      log: message => this.output.info(message),
      trace: trace ? (direction, json) => this.output.trace(`${direction === 'send' ? '-->' : '<--'} ${json}`) : undefined,
    });
    this.client = client;

    client.onEvent(event => {
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
      await client.ready();
      this.output.info(`Indexing server started (pid ${client.pid}).`);
      await this.registerWorkspace();
    } catch (e) {
      this.output.error(`Error starting indexing server: ${errorMessage(e)}`);
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

  async showStatus(): Promise<void> {
    const server = this.server;
    if (!server) {
      const choice = await vscode.window.showWarningMessage(
        'VsChromium: the indexing server is not running.', 'Restart Server', 'Show Output');
      if (choice === 'Restart Server') {
        await this.restart();
      } else if (choice === 'Show Output') {
        this.output.show();
      }
      return;
    }
    try {
      const stats = await server.request({ $type: 'GetDatabaseStatisticsRequest' });
      const mb = (bytes: number) => `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
      const lines = [
        `Status: ${stats.serverStatus}`,
        `Projects: ${stats.projectCount}`,
        `Files: ${stats.fileCount.toLocaleString()} (${stats.searchableFileCount.toLocaleString()} searchable)`,
        `Memory: ${mb(stats.serverGcMemoryUsage)} managed, ${mb(stats.serverNativeMemoryUsage)} native`,
        `Index last updated: ${new Date(stats.indexLastUpdatedUtc).toLocaleString()}`,
      ];
      await vscode.window.showInformationMessage('VsChromium indexing server', { modal: true, detail: lines.join('\n') });
    } catch (e) {
      void vscode.window.showErrorMessage(`VsChromium: ${errorMessage(e)}`);
    }
  }

  dispose(): void {
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
    // The server sends indexing events only if it found a project for one of the paths.
    const stats = await this.server?.request({ $type: 'GetDatabaseStatisticsRequest' });
    if (stats && stats.projectCount === 0 && this.state.kind === 'starting') {
      this.setState({ kind: 'noProject' });
    }
  }

  private async onWorkspaceFoldersChanged(e: vscode.WorkspaceFoldersChangeEvent) {
    for (const folder of e.added) {
      await this.registerPath(folder.uri);
    }
    for (const folder of e.removed) {
      await this.unregisterPath(folder.uri);
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
      await this.server.request({ $type: 'RegisterFileRequest', fileName: uri.fsPath });
    } catch (e) {
      this.output.warn(`Error registering "${uri.fsPath}": ${errorMessage(e)}`);
    }
  }

  private async unregisterPath(uri: vscode.Uri) {
    if (uri.scheme !== 'file' || !this.server) {
      return;
    }
    try {
      await this.server.request({ $type: 'UnregisterFileRequest', fileName: uri.fsPath });
    } catch (e) {
      this.output.warn(`Error unregistering "${uri.fsPath}": ${errorMessage(e)}`);
    }
  }

  private async refreshStatistics() {
    try {
      const stats = await this.server?.request({ $type: 'GetDatabaseStatisticsRequest' });
      if (stats && this.state.kind === 'ready') {
        this.setState({ kind: 'ready', fileCount: stats.fileCount, projectCount: stats.projectCount });
      }
    } catch (e) {
      this.output.warn(`Error getting server statistics: ${errorMessage(e)}`);
    }
  }

  private setState(state: IndexingState) {
    this.state = state;
    const { text, tooltip } = describeIndexingState(state);
    this.statusBar.text = text;
    this.statusBar.tooltip = tooltip;
    this.statusBar.command = state.kind === 'stopped' ? 'vschromium.restartServer' : 'vschromium.showServerStatus';
  }
}

function errorMessage(e: unknown): string {
  return e instanceof Error ? e.message : String(e);
}

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const output = vscode.window.createOutputChannel('VsChromium', { log: true });
  const manager = new ServerManager(context, output);
  const codeSearch = new CodeSearchViewProvider(context.extensionUri, () => manager.server, output);
  const fileNames = new FileNamesPick(() => manager.server, output);
  context.subscriptions.push(
    output,
    manager,
    vscode.window.registerWebviewViewProvider(CodeSearchViewProvider.viewType, codeSearch, {
      webviewOptions: { retainContextWhenHidden: true },
    }),
    vscode.commands.registerCommand('vschromium.searchCode', () => codeSearch.show(getSearchTextFromEditor())),
    vscode.commands.registerCommand('vschromium.findFileNames', () => fileNames.show(getSelectedText())),
    vscode.commands.registerCommand('vschromium.showServerStatus', () => manager.showStatus()),
    vscode.commands.registerCommand('vschromium.restartServer', () => manager.restart()),
    vscode.commands.registerCommand('vschromium.showOutput', () => output.show()),
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
