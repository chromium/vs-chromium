// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as crypto from 'crypto';
import * as fs from 'fs';
import * as os from 'os';
import * as vscode from 'vscode';
import { IndexClearCacheRequest, ProjectClearCacheRequest } from '../../protocol/protocol.g';
import { DashboardBridge, DashboardServer } from './dashboardBridge';
import { HostCommand, PageToHost } from './dashboardMessages';
import { serverLogDirectory } from './logDirectory';

/** What the dashboard needs from the extension (the server manager). */
export interface DashboardServices {
  getServer(): DashboardServer | undefined;
  restartServer(): Promise<void>;
  showOutput(): void;
  trigramFiltersSetting(): boolean;
  setTrigramFiltersSetting(enabled: boolean): Promise<void>;
  setProjectTrigramFilters(projectPath: string, enabled: boolean): Promise<void>;
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
        case 'openCacheSettings':
          await vscode.commands.executeCommand('workbench.action.openSettings', 'vschromium.cache');
          break;
      }
    } catch (e) {
      void vscode.window.showErrorMessage(`VsChromium: ${this.services.errorMessage(e)}`);
    }
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
