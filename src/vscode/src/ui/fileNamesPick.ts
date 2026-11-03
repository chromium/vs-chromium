// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { SearchPathsRequest } from '../protocol/protocol.g';
import { ServerClient, userErrorMessage } from '../server/client';
import { flattenFilePaths } from './codeSearchModel';
import { FilePathQuery, parseFilePathQuery } from './filePathQuery';

interface FileItem extends vscode.QuickPickItem {
  fullPath: string;
}

/**
 * "Find File Names": a quick pick searching the file paths of the index as the user types.
 * Understands paths with positions (e.g. "base/foo.cc:19:18" from build output) and opens the
 * file at that position.
 */
export class FileNamesPick {
  constructor(
    private readonly getServer: () => ServerClient | undefined,
    private readonly output: vscode.LogOutputChannel,
  ) {}

  show(initialValue: string | undefined): void {
    const server = this.getServer();
    if (!server) {
      void vscode.window.showWarningMessage('VsChromium: the indexing server is not running.');
      return;
    }
    const config = vscode.workspace.getConfiguration('vschromium');
    const maxResults = config.get<number>('searchFilePaths.maxResults') ?? 2000;
    const delay = config.get<number>('searchCode.autoSearchDelayMs') ?? 100;

    const pick = vscode.window.createQuickPick<FileItem>();
    pick.title = 'VsChromium: Find File Names';
    pick.placeholder = 'File name or path (e.g. "foo.cc", "base/*.h", "base/foo.cc:19" from build output)';
    pick.matchOnDescription = true;

    let searchId = 0;
    let timer: NodeJS.Timeout | undefined;
    let query: FilePathQuery = { pattern: '' };
    // Cancels the running search when a new search starts or the pick is closed.
    let cancellation: vscode.CancellationTokenSource | undefined;
    const cancelSearch = () => {
      cancellation?.cancel();
      cancellation?.dispose();
      cancellation = undefined;
    };

    const search = async () => {
      const id = ++searchId;
      cancelSearch();
      query = parseFilePathQuery(pick.value);
      if (query.pattern.length === 0) {
        pick.items = [];
        pick.busy = false;
        pick.title = 'VsChromium: Find File Names';
        return;
      }
      pick.busy = true;
      const token = (cancellation = new vscode.CancellationTokenSource()).token;
      try {
        const current = this.getServer();
        if (!current) {
          throw new Error('The indexing server is not running.');
        }
        const response = await current.request(SearchPathsRequest.type, {
          query: { text: query.pattern, includeSymLinks: true },
          maxResults,
        }, token);
        if (id !== searchId) {
          return; // A newer search is in progress.
        }
        const items: FileItem[] = flattenFilePaths(response).map(f => ({
          label: path.basename(f.relativePath),
          description: path.dirname(f.relativePath) === '.' ? '' : path.dirname(f.relativePath),
          detail: undefined,
          fullPath: f.fullPath,
          // Results are already filtered by the server: never hide them.
          alwaysShow: true,
        }));
        const direct = directFileItem(query.pattern);
        if (direct && !items.some(i => i.fullPath.toLowerCase() === direct.fullPath.toLowerCase())) {
          items.unshift(direct);
        }
        const position = query.line !== undefined ? ` (line ${query.line + 1})` : '';
        const truncated = response.hitCount >= maxResults ? ` (limited to ${maxResults.toLocaleString()})` : '';
        pick.title = `VsChromium: ${response.hitCount.toLocaleString()} file(s)${truncated} of ` +
          `${response.totalCount.toLocaleString()}${position}`;
        pick.items = items;
      } catch (e) {
        const message = userErrorMessage(e);
        if (id === searchId && message !== undefined) {
          pick.items = [];
          pick.title = `VsChromium: ${message}`;
        }
      } finally {
        if (id === searchId) {
          pick.busy = false;
        }
      }
    };

    pick.onDidChangeValue(() => {
      clearTimeout(timer);
      timer = setTimeout(() => void search(), delay);
    });
    pick.onDidAccept(async () => {
      const item = pick.selectedItems[0] ?? pick.activeItems[0];
      if (!item) {
        return;
      }
      pick.hide();
      await openFile(item.fullPath, query, this.output);
    });
    pick.onDidHide(() => {
      clearTimeout(timer);
      cancelSearch();
      pick.dispose();
    });

    pick.show();
    if (initialValue) {
      pick.value = initialValue;
      void search();
    }
  }
}

/** Item opening `pattern` directly, if it is the absolute path of an existing file. */
function directFileItem(pattern: string): FileItem | undefined {
  if (!path.isAbsolute(pattern) || /[*?]/.test(pattern)) {
    return undefined;
  }
  try {
    if (fs.statSync(pattern).isFile()) {
      return { label: path.basename(pattern), description: path.dirname(pattern), fullPath: pattern, alwaysShow: true };
    }
  } catch {
    // Not an existing file.
  }
  return undefined;
}

async function openFile(fullPath: string, query: FilePathQuery, output: vscode.LogOutputChannel) {
  try {
    const document = await vscode.workspace.openTextDocument(vscode.Uri.file(fullPath));
    let selection: vscode.Range | undefined;
    if (query.line !== undefined) {
      const line = Math.min(query.line, document.lineCount - 1);
      const column = Math.min(query.column ?? 0, document.lineAt(line).text.length);
      const position = new vscode.Position(line, column);
      selection = new vscode.Range(position, position);
    }
    await vscode.window.showTextDocument(document, { selection, preview: false });
  } catch (e) {
    const message = e instanceof Error ? e.message : String(e);
    output.warn(`Error opening "${fullPath}": ${message}`);
    void vscode.window.showErrorMessage(`VsChromium: cannot open "${fullPath}": ${message}`);
  }
}
