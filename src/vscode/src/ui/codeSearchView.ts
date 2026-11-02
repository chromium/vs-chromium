// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as crypto from 'crypto';
import * as vscode from 'vscode';
import { ServerClient } from '../server/client';
import { SearchCodeResponse } from '../server/messages';
import { describeResults, flattenSearchResults, SearchResultFile, toDisplayLine } from './codeSearchModel';
import { FromWebview, SearchOptions, ToWebview } from './codeSearchProtocol';

/**
 * Webview view implementing the "Code Search" panel: search box with options, results grouped by
 * file (text extracts are loaded lazily, as files become visible), click to open at the match.
 */
export class CodeSearchViewProvider implements vscode.WebviewViewProvider {
  static readonly viewType = 'vschromium.codeSearch';

  private view: vscode.WebviewView | undefined;
  private webviewReady = false;
  private pendingQuery: { query: string; search: boolean } | undefined;
  private lastSearch: { searchId: number; files: SearchResultFile[] } | undefined;

  constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly getServer: () => ServerClient | undefined,
    private readonly output: vscode.LogOutputChannel,
  ) {}

  resolveWebviewView(view: vscode.WebviewView): void {
    this.view = view;
    this.webviewReady = false;
    const mediaUri = vscode.Uri.joinPath(this.extensionUri, 'media');
    view.webview.options = { enableScripts: true, localResourceRoots: [mediaUri] };
    view.webview.html = this.getHtml(view.webview, mediaUri);
    view.webview.onDidReceiveMessage((message: FromWebview) => this.onMessage(message));
    view.onDidDispose(() => {
      this.view = undefined;
      this.webviewReady = false;
    });
  }

  /** Reveals the view and sets the search text (e.g. the editor selection). */
  async show(query: string | undefined): Promise<void> {
    if (query !== undefined) {
      this.pendingQuery = { query, search: query.length > 0 };
    }
    await vscode.commands.executeCommand(`${CodeSearchViewProvider.viewType}.focus`);
    this.flushPendingQuery();
  }

  /** Updates the status line (e.g. when the server state changes). */
  setStatus(text: string, busy: boolean): void {
    this.post({ type: 'status', text, busy });
  }

  private flushPendingQuery() {
    if (this.webviewReady && this.pendingQuery) {
      this.post({ type: 'setQuery', ...this.pendingQuery });
      this.pendingQuery = undefined;
    }
  }

  private async onMessage(message: FromWebview) {
    switch (message.type) {
      case 'ready':
        this.webviewReady = true;
        this.flushPendingQuery();
        break;
      case 'search':
        await this.search(message.searchId, message.options);
        break;
      case 'getExtracts':
        await this.getExtracts(message.searchId, message.fileIndex);
        break;
      case 'open':
        await this.open(message.searchId, message.fileIndex, message.positionIndex, message.preview);
        break;
    }
  }

  private async search(searchId: number, options: SearchOptions) {
    const server = this.getServer();
    if (!server) {
      this.post({ type: 'error', searchId, message: 'The indexing server is not running.' });
      return;
    }
    const config = vscode.workspace.getConfiguration('vschromium.searchCode');
    const maxResults = config.get<number>('maxResults') ?? 10000;
    const started = Date.now();
    let response: SearchCodeResponse;
    try {
      response = await server.request({
        $type: 'SearchCodeRequest',
        searchParams: {
          searchString: options.query,
          filePathPattern: options.filePathPattern,
          maxResults,
          matchCase: options.matchCase,
          matchWholeWord: options.matchWholeWord,
          regex: options.regex,
          includeSymLinks: true,
          useRe2Engine: true,
        },
      });
    } catch (e) {
      this.post({ type: 'error', searchId, message: errorMessage(e) });
      return;
    }
    const files = flattenSearchResults(response);
    this.lastSearch = { searchId, files };
    this.post({
      type: 'results',
      searchId,
      summary: describeResults(response, files.length, maxResults, Date.now() - started),
      files: files.map(f => ({
        index: f.index,
        relativePath: f.relativePath,
        fullPath: f.fullPath,
        matchCount: f.positions.length,
      })),
    });
  }

  private async getExtracts(searchId: number, fileIndex: number) {
    const file = this.getFile(searchId, fileIndex);
    const server = this.getServer();
    if (!file || !server || file.positions.length === 0) {
      return;
    }
    const maxExtractLength = vscode.workspace.getConfiguration('vschromium.searchCode').get<number>('maxExtractLength') ?? 120;
    try {
      const response = await server.request({
        $type: 'GetFileExtractsRequest',
        fileName: file.fullPath,
        positions: file.positions,
        maxExtractLength,
      });
      const lines = file.positions.map((position, i) => {
        const extract = response.fileExtracts[i];
        return extract ? toDisplayLine(extract, position) : null;
      });
      this.post({ type: 'extracts', searchId, fileIndex, lines });
    } catch (e) {
      this.output.warn(`Error getting text extracts for "${file.fullPath}": ${errorMessage(e)}`);
    }
  }

  private async open(searchId: number, fileIndex: number, positionIndex: number, preview: boolean) {
    const file = this.getFile(searchId, fileIndex);
    if (!file) {
      return;
    }
    try {
      const document = await vscode.workspace.openTextDocument(vscode.Uri.file(file.fullPath));
      const span = file.positions[positionIndex];
      // Server positions are UTF-16 character offsets, like VS Code document offsets.
      const selection = span
        ? new vscode.Selection(document.positionAt(span.position + span.length), document.positionAt(span.position))
        : undefined;
      await vscode.window.showTextDocument(document, { selection, preview, preserveFocus: preview });
    } catch (e) {
      void vscode.window.showErrorMessage(`VsChromium: cannot open "${file.fullPath}": ${errorMessage(e)}`);
    }
  }

  private getFile(searchId: number, fileIndex: number): SearchResultFile | undefined {
    if (this.lastSearch?.searchId !== searchId) {
      return undefined; // Stale request from a previous search.
    }
    return this.lastSearch.files[fileIndex];
  }

  private post(message: ToWebview) {
    void this.view?.webview.postMessage(message);
  }

  private getHtml(webview: vscode.Webview, mediaUri: vscode.Uri): string {
    const nonce = crypto.randomBytes(16).toString('base64');
    const scriptUri = webview.asWebviewUri(vscode.Uri.joinPath(mediaUri, 'codeSearch.js'));
    const styleUri = webview.asWebviewUri(vscode.Uri.joinPath(mediaUri, 'codeSearch.css'));
    const delay = vscode.workspace.getConfiguration('vschromium.searchCode').get<number>('autoSearchDelayMs') ?? 100;
    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource}; script-src 'nonce-${nonce}';">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <link href="${styleUri}" rel="stylesheet">
  <title>Code Search</title>
</head>
<body data-auto-search-delay="${delay}">
  <div class="toolbar">
    <div class="input-row">
      <input id="query" type="text" placeholder="Search code (Enter to search)" spellcheck="false" aria-label="Search code">
      <button id="matchCase" class="toggle" title="Match Case" aria-pressed="false">Aa</button>
      <button id="matchWholeWord" class="toggle" title="Match Whole Word" aria-pressed="false"><u>ab</u></button>
      <button id="regex" class="toggle" title="Use Regular Expression" aria-pressed="false">.*</button>
    </div>
    <div class="input-row">
      <input id="filePathPattern" type="text" placeholder="Files to include, e.g. *.cc;*.h;-*_test.cc" spellcheck="false" aria-label="File path pattern">
    </div>
    <div id="status" class="status"></div>
  </div>
  <div id="results" class="results" role="tree"></div>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }
}

function errorMessage(e: unknown): string {
  return e instanceof Error ? e.message : String(e);
}
