// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Messages exchanged between the Code Search webview (media/codeSearch.js) and the extension.

import { DisplayLine } from './codeSearchModel';

export interface SearchOptions {
  query: string;
  filePathPattern: string;
  matchCase: boolean;
  matchWholeWord: boolean;
  regex: boolean;
}

/** Messages sent by the webview to the extension. */
export type FromWebview =
  | { type: 'ready' }
  | { type: 'search'; searchId: number; options: SearchOptions }
  | { type: 'getExtracts'; searchId: number; fileIndex: number }
  | { type: 'open'; searchId: number; fileIndex: number; positionIndex: number; preview: boolean };

/** A file in the results, as sent to the webview. */
export interface WebviewFile {
  index: number;
  relativePath: string;
  fullPath: string;
  matchCount: number;
}

/** Messages sent by the extension to the webview. */
export type ToWebview =
  | { type: 'setQuery'; query: string; search: boolean }
  | { type: 'status'; text: string; busy: boolean }
  | { type: 'results'; searchId: number; summary: string; files: WebviewFile[] }
  | { type: 'error'; searchId: number; message: string }
  | { type: 'extracts'; searchId: number; fileIndex: number; lines: (DisplayLine | null)[] };
