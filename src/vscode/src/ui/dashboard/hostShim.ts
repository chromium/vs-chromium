// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import type { HostToPage, PageToHost } from './dashboardMessages';

// The only link between the dashboard page and its host: `post(message)` and
// `onMessage(handler)`, on top of the messaging API of the browser control hosting the page,
// selected at runtime:
// - VS Code webviews: `acquireVsCodeApi().postMessage`, messages received as "message" events of
//   the window;
// - WebView2 (Visual Studio tool windows): `window.chrome.webview.postMessage`, messages received
//   as "message" events of `window.chrome.webview`.
// Kept free of DOM types so that it can be unit tested with a fake window.

type MessageListener = (event: { data: unknown }) => void;

interface MessageEventTarget {
  addEventListener(type: 'message', listener: MessageListener): void;
  removeEventListener(type: 'message', listener: MessageListener): void;
}

/** The part of `window` used by the shim. */
export interface ShimWindow extends MessageEventTarget {
  acquireVsCodeApi?: () => { postMessage(message: unknown): void };
  chrome?: { webview?: MessageEventTarget & { postMessage(message: unknown): void } };
}

export type HostKind = 'vscode' | 'webview2';

export interface HostShim {
  readonly kind: HostKind;
  post(message: PageToHost): void;
  /** Registers a handler of the messages of the host. Returns a function to unregister it. */
  onMessage(handler: (message: HostToPage) => void): () => void;
}

/** Creates the shim for the host of the page. Throws if the page is not hosted. */
export function createHostShim(win: ShimWindow): HostShim {
  if (typeof win.acquireVsCodeApi === 'function') {
    // Note: acquireVsCodeApi can only be called once per page.
    const api = win.acquireVsCodeApi();
    return {
      kind: 'vscode',
      post: message => api.postMessage(message),
      onMessage: handler => listen(win, handler),
    };
  }
  const webview = win.chrome?.webview;
  if (webview && typeof webview.postMessage === 'function') {
    return {
      kind: 'webview2',
      post: message => webview.postMessage(message),
      onMessage: handler => listen(webview, handler),
    };
  }
  throw new Error('The dashboard page is not hosted (no VS Code or WebView2 messaging API).');
}

function listen(target: MessageEventTarget, handler: (message: HostToPage) => void): () => void {
  const listener: MessageListener = event => {
    const message = event.data as HostToPage | undefined;
    if (message && typeof message === 'object' && typeof message.type === 'string') {
      handler(message);
    }
  };
  target.addEventListener('message', listener);
  return () => target.removeEventListener('message', listener);
}
