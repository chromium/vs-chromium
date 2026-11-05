// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as crypto from 'crypto';
import * as fs from 'fs';
import * as os from 'os';
import * as vscode from 'vscode';
import {
  IndexClearCacheRequest,
  ProjectAddIgnorePatternRequest,
  ProjectClearCacheRequest,
  ProjectDetailsRequest,
  ProjectRebuildRequest,
} from '../../protocol/protocol.g';
import { DashboardBridge, DashboardServer } from './dashboardBridge';
import { DashboardIgnoreSection, HostCommand, PageToHost } from './dashboardMessages';
import { serverLogDirectory } from './logDirectory';

/** What the dashboard needs from the extension (the server manager). */
export interface DashboardServices {
  getServer(): DashboardServer | undefined;
  restartServer(): Promise<void>;
  showOutput(): void;
  trigramFiltersSetting(): boolean;
  setTrigramFiltersSetting(enabled: boolean): Promise<void>;
  setProjectTrigramFilters(projectPath: string, enabled: boolean): Promise<void>;
  /** Sets the cache setting of a project (disabling it deletes its cache, after a confirmation). */
  setProjectCacheEnabled(projectPath: string, enabled: boolean): Promise<void>;
  errorMessage(e: unknown): string;
  /** The cache settings (applied when the server restarts). */
  cacheSettings(): { enabled: boolean; directory: string };
}

/**
 * The "VsChromium Server" editor tab: a webview panel hosting the dashboard page
 * (src/ui/dashboard/page, bundled into out/media/dashboard.js). Single instance: `show` reveals
 * the existing tab. The page refreshes its data while the tab is visible.
 */
export class DashboardPanel implements vscode.Disposable {
  static readonly viewType = 'vschromium.serverDashboard';

  private panel: vscode.WebviewPanel | undefined;
  private bridge: DashboardBridge | undefined;
  private readonly logDirectory = serverLogDirectory(process.platform, process.env, os.homedir());

  constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly services: DashboardServices,
  ) {}

  show(): void {
    if (this.panel) {
      this.panel.reveal();
      return;
    }
    const mediaRoots = [
      vscode.Uri.joinPath(this.extensionUri, 'media'),
      vscode.Uri.joinPath(this.extensionUri, 'out', 'media'),
    ];
    const panel = vscode.window.createWebviewPanel(DashboardPanel.viewType, 'VsChromium Server', vscode.ViewColumn.Active, {
      enableScripts: true,
      localResourceRoots: mediaRoots,
      // Note: The page stops refreshing when hidden; keeping it avoids a reload when shown again.
      retainContextWhenHidden: true,
    });
    const bridge = new DashboardBridge({
      hostName: 'VS Code',
      getServer: () => this.services.getServer(),
      post: message => void panel.webview.postMessage(message),
      trigramFiltersSetting: () => this.services.trigramFiltersSetting(),
      logDirectory: this.logDirectory,
      cacheSettings: () => this.services.cacheSettings(),
      executeCommand: command => this.executeCommand(command),
      errorMessage: e => this.services.errorMessage(e),
    });
    this.panel = panel;
    this.bridge = bridge;
    const disposables: vscode.Disposable[] = [
      panel.webview.onDidReceiveMessage((message: PageToHost) => {
        void bridge.handleMessage(message);
      }),
      panel.onDidChangeViewState(e => {
        void panel.webview.postMessage({ type: 'visibility', visible: e.webviewPanel.visible });
        if (e.webviewPanel.visible) {
          bridge.syncServer();
        }
      }),
      vscode.workspace.onDidChangeConfiguration(e => {
        if (e.affectsConfiguration('vschromium.trigramFilters') || e.affectsConfiguration('vschromium.cacheEnabled') ||
            e.affectsConfiguration('vschromium.cacheDirectory')) {
          bridge.postHostState();
        }
      }),
    ];
    panel.onDidDispose(() => {
      bridge.dispose();
      disposables.forEach(d => d.dispose());
      if (this.panel === panel) {
        this.panel = undefined;
        this.bridge = undefined;
      }
    });
    panel.webview.html = this.getHtml(panel.webview);
  }

  /** Tells the page that the server changed (e.g. restarted). */
  serverChanged(): void {
    this.bridge?.syncServer();
  }

  dispose(): void {
    this.panel?.dispose();
  }

  private async executeCommand(command: HostCommand): Promise<void> {
    try {
      switch (command.command) {
        case 'restartServer':
          await this.services.restartServer();
          break;
        case 'showOutput':
          this.services.showOutput();
          break;
        case 'openLogDirectory':
          if (!fs.existsSync(this.logDirectory)) {
            void vscode.window.showInformationMessage(`VsChromium: no log files yet (${this.logDirectory}).`);
          } else {
            await vscode.env.openExternal(vscode.Uri.file(this.logDirectory));
          }
          break;
        case 'setTrigramFiltersSetting':
          await this.services.setTrigramFiltersSetting(command.enabled);
          break;
        case 'setProjectTrigramFilters':
          await this.services.setProjectTrigramFilters(command.projectPath, command.enabled);
          break;
        case 'clearCache':
          await this.clearCache(command.projectPath);
          break;
        case 'rebuildProject':
          await this.rebuildProject(command.projectPath);
          break;
        case 'setProjectCacheEnabled':
          await this.services.setProjectCacheEnabled(command.projectPath, command.enabled);
          break;
        case 'openProjectCacheFolder':
          await this.openProjectCacheFolder(command.projectPath);
          break;
        case 'openCacheSettings':
          await vscode.commands.executeCommand('workbench.action.openSettings', 'vschromium.cache');
          break;
        case 'copyToClipboard':
          await vscode.env.clipboard.writeText(command.text);
          break;
        case 'addIgnorePattern':
          await this.addIgnorePattern(command.projectPath, command.section, command.pattern);
          break;
      }
    } catch (e) {
      void vscode.window.showErrorMessage(`VsChromium: ${this.services.errorMessage(e)}`);
    }
  }

  /** Adds an ignore pattern to `vs-chromium-project.txt`, after a confirmation. */
  private async addIgnorePattern(projectPath: string, section: DashboardIgnoreSection, pattern: string): Promise<void> {
    const sectionName = section === 'sourceExplorerIgnore' ? 'SourceExplorer.ignore' : 'SearchableFiles.ignore';
    const effect = section === 'sourceExplorerIgnore'
      ? 'Matching files and directories will be excluded from the project tree, file name search, and code search.'
      : 'Matching files will remain in the project tree and file name search, but their contents will be excluded from code search.';
    const confirmLabel = 'Add Rule';
    const choice = await vscode.window.showWarningMessage(
      `Add "${pattern}" to [${sectionName}] in "${projectPath}"?`,
      {
        modal: true,
        detail: `${effect}\n\nUpdating vs-chromium-project.txt triggers a rescan of the project.`,
      },
      confirmLabel);
    if (choice !== confirmLabel) {
      return;
    }
    const server = this.services.getServer();
    if (!server) {
      throw new Error('the indexing server is not running.');
    }
    const result = await server.request(ProjectAddIgnorePatternRequest.type, { projectPath, section, pattern });
    if (!result.added) {
      void vscode.window.showInformationMessage(
        `VsChromium: "${pattern}" is already present in [${result.sectionName}] (${result.projectFile}).`);
    }
  }

  /** Rebuilds the index of a project (its cache is deleted, its files scanned and read again), after a confirmation. */
  private async rebuildProject(projectPath: string): Promise<void> {
    const rebuild = 'Rebuild Index';
    const choice = await vscode.window.showWarningMessage(
      `Rebuild the index of "${projectPath}"?`,
      {
        modal: true,
        detail: 'The cache of the project is deleted, then its files are scanned and read again. ' +
          'Searches use the current index until the new one is ready. The other projects are not affected.',
      },
      rebuild);
    if (choice !== rebuild) {
      return;
    }
    const server = this.services.getServer();
    if (!server) {
      throw new Error('the indexing server is not running.');
    }
    await server.request(ProjectRebuildRequest.type, { projectPath });
  }

  /** Opens the cache directory of a project (as reported by the server, not by the page). */
  private async openProjectCacheFolder(projectPath: string): Promise<void> {
    const server = this.services.getServer();
    if (!server) {
      throw new Error('the indexing server is not running.');
    }
    const details = await server.request(ProjectDetailsRequest.type, {
      projectPath,
      maxFilesByExtensionDetailsCount: 0,
      maxLargeFilesDetailsCount: 0,
    });
    const directory = details.cache.directory;
    if (!directory || !fs.existsSync(directory)) {
      void vscode.window.showInformationMessage(`VsChromium: the project "${projectPath}" has no cache directory yet.`);
      return;
    }
    await vscode.env.openExternal(vscode.Uri.file(directory));
  }

  /** Deletes the index cache of a project (or of all the projects), after a confirmation. */
  private async clearCache(projectPath: string | undefined): Promise<void> {
    const clear = 'Clear Cache';
    const choice = await vscode.window.showWarningMessage(
      projectPath ? `Clear the index cache of "${projectPath}"?` : 'Clear the index cache of all the projects?',
      {
        modal: true,
        detail: 'The cache files are deleted. The current index stays in use; the cache is written again ' +
          'once the index settles. Until then, a restarted server indexes the files from disk.',
      },
      clear);
    if (choice !== clear) {
      return;
    }
    const server = this.services.getServer();
    if (!server) {
      throw new Error('the indexing server is not running.');
    }
    const result = projectPath !== undefined
      ? await server.request(ProjectClearCacheRequest.type, { projectPath })
      : await server.request0(IndexClearCacheRequest.type);
    void vscode.window.showInformationMessage(
      `VsChromium: index cache cleared (${result.projectCount} ${result.projectCount === 1 ? 'project' : 'projects'}).`);
  }

  private getHtml(webview: vscode.Webview): string {
    const nonce = crypto.randomBytes(16).toString('base64');
    const scriptUri = webview.asWebviewUri(vscode.Uri.joinPath(this.extensionUri, 'out', 'media', 'dashboard.js'));
    const styleUri = webview.asWebviewUri(vscode.Uri.joinPath(this.extensionUri, 'media', 'dashboard.css'));
    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource}; script-src 'nonce-${nonce}';">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <link href="${styleUri}" rel="stylesheet">
  <title>VsChromium Server</title>
</head>
<body>
  <div id="app"></div>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }
}
