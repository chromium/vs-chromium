// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { ServerStatisticsRequest } from '../protocol/protocol.g';
import { ServerClient } from '../server/client';
import { findServerPath, serverArguments, ServerRestartSettings } from '../server/serverLocator';

describe('serverArguments', () => {
  it('uses the default cache when nothing is set', () => {
    assert.deepEqual(serverArguments({}), ['--jsonrpc']);
    assert.deepEqual(serverArguments({ cacheEnabled: true, cacheDirectory: '' }), ['--jsonrpc']);
    assert.deepEqual(serverArguments({ cacheEnabled: true, cacheDirectory: '   ' }), ['--jsonrpc']);
  });

  it('passes the cache directory (trimmed)', () => {
    assert.deepEqual(serverArguments({ cacheDirectory: '  D:\\Cache Dir\\VsChromium ' }), ['--jsonrpc', '--cache-dir', 'D:\\Cache Dir\\VsChromium']);
    assert.deepEqual(serverArguments({ cacheEnabled: true, cacheDirectory: '/tmp/cache' }), ['--jsonrpc', '--cache-dir', '/tmp/cache']);
  });

  it('disables the cache, whatever the directory', () => {
    assert.deepEqual(serverArguments({ cacheEnabled: false }), ['--jsonrpc', '--no-cache']);
    assert.deepEqual(serverArguments({ cacheEnabled: false, cacheDirectory: 'D:\\Cache' }), ['--jsonrpc', '--no-cache']);
  });

  it('lists the settings applied when the server starts', () => {
    assert.deepEqual([...ServerRestartSettings].sort(), ['vschromium.cacheDirectory', 'vschromium.cacheEnabled']);
  });
});

const extensionRoot = path.resolve(__dirname, '..', '..');
const serverPath = process.env.VSCHROMIUM_SERVER_PATH ||
  findServerPath(extensionRoot, undefined, /*preferRepositoryBuild*/ true);

describe('serverArguments with the server', { skip: serverPath ? false : 'VsChromium.Server.exe not built' }, () => {
  async function cacheStatistics(args: string[]) {
    const client = ServerClient.start(serverPath!, { args, clientName: 'VS Code tests' });
    try {
      await client.ready();
      return (await client.request(ServerStatisticsRequest.type, {})).indexCache;
    } finally {
      await client.dispose();
    }
  }

  it('starts the server with a cache directory', async () => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'vschromium-cache-setting-'));
    try {
      const cache = await cacheStatistics(serverArguments({ cacheDirectory: directory }));
      assert.equal(cache?.enabled, true);
      assert.equal(cache?.cacheDirectory, directory);
    } finally {
      fs.rmSync(directory, { recursive: true, force: true });
    }
  });

  it('starts the server without cache', async () => {
    const cache = await cacheStatistics(serverArguments({ cacheEnabled: false }));
    assert.equal(cache?.enabled, false);
  });
});
