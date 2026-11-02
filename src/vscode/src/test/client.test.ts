// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Integration tests of ServerClient against the real VsChromium.Server.exe built in
// <repo>/Binaries/Debug (skipped if the server has not been built).

import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { after, before, describe, it } from 'node:test';
import { ServerClient, ServerExitedError } from '../server/client';
import { TypedEvent } from '../server/messages';
import { findServerPath } from '../server/serverLocator';
import { flattenSearchResults, toDisplayLine } from '../ui/codeSearchModel';

const extensionRoot = path.resolve(__dirname, '..', '..');
const repoRoot = path.resolve(extensionRoot, '..', '..');
const serverPath = findServerPath(extensionRoot, undefined);
const testEnlistmentFile = path.join(repoRoot, 'src', 'Tests', 'TestData', 'chromium', 'src', 'PRESUBMIT.py');

function waitForEvent(client: ServerClient, type: TypedEvent['$type'], timeoutMs = 10000): Promise<TypedEvent> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      unregister();
      reject(new Error(`Timeout waiting for event ${type}`));
    }, timeoutMs);
    const unregister = client.onEvent(event => {
      if (event.$type === type) {
        clearTimeout(timer);
        unregister();
        resolve(event);
      }
    });
  });
}

describe('ServerClient', { skip: serverPath ? false : 'VsChromium.Server.exe not built' }, () => {
  let client: ServerClient;
  const logs: string[] = [];

  before(async () => {
    client = new ServerClient(serverPath!, { log: m => logs.push(m) });
    await client.ready();
  });

  after(async () => {
    await client.dispose();
  });

  it('registers a project and reports indexing events and statistics', async () => {
    assert.ok(fs.existsSync(testEnlistmentFile), `Missing test data: ${testEnlistmentFile}`);
    const loaded = waitForEvent(client, 'SearchEngineFilesLoaded');

    const done = await client.request({ $type: 'RegisterFileRequest', fileName: testEnlistmentFile });
    assert.equal(done.$type, 'DoneResponse');
    await loaded;

    const stats = await client.request({ $type: 'GetDatabaseStatisticsRequest' });
    assert.equal(stats.$type, 'GetDatabaseStatisticsResponse');
    assert.equal(stats.projectCount, 1);
    assert.ok(stats.fileCount > 0, 'fileCount should be > 0');
    assert.ok(['Idle', 'Paused', 'Yield', 'Busy'].includes(stats.serverStatus));
  });

  it('searches code and returns text extracts for the matches', async () => {
    const response = await client.request({
      $type: 'SearchCodeRequest',
      searchParams: { searchString: 'looking like', maxResults: 100, matchCase: true },
    });
    assert.equal(response.$type, 'SearchCodeResponse');
    assert.equal(response.hitCount, 1);

    const files = flattenSearchResults(response);
    assert.equal(files.length, 1);
    assert.equal(files[0].fullPath, path.join(path.dirname(testEnlistmentFile), 'readme.txt'));

    const extracts = await client.request({
      $type: 'GetFileExtractsRequest',
      fileName: files[0].fullPath,
      positions: files[0].positions,
      maxExtractLength: 120,
    });
    assert.equal(extracts.fileExtracts.length, 1);
    const line = toDisplayLine(extracts.fileExtracts[0]!, files[0].positions[0]);
    assert.deepEqual(line, {
      lineNumber: 1,
      columnNumber: 16,
      before: 'Test directory ',
      match: 'looking like',
      after: ' a local Chromium enlistment.',
    });
  });

  it('returns an empty result for a search without matches', async () => {
    const response = await client.request({
      $type: 'SearchCodeRequest',
      searchParams: { searchString: 'this text does not exist anywhere 1234', maxResults: 100 },
    });
    assert.equal(response.hitCount, 0);
    assert.deepEqual(flattenSearchResults(response), []);
  });

  it('runs concurrent requests', async () => {
    const results = await Promise.all(
      [1, 2, 3, 4, 5].map(() => client.request({ $type: 'GetDatabaseStatisticsRequest' })));
    for (const r of results) {
      assert.equal(r.$type, 'GetDatabaseStatisticsResponse');
    }
  });
});

describe('ServerClient workspace folder registration', { skip: serverPath ? false : 'VsChromium.Server.exe not built' }, () => {
  it('discovers the project when registering a folder (as done for VS Code workspace folders)', async () => {
    const client = new ServerClient(serverPath!);
    try {
      await client.ready();
      const loaded = waitForEvent(client, 'SearchEngineFilesLoaded');
      await client.request({ $type: 'RegisterFileRequest', fileName: path.dirname(testEnlistmentFile) });
      await loaded;
      const stats = await client.request({ $type: 'GetDatabaseStatisticsRequest' });
      assert.equal(stats.projectCount, 1);
    } finally {
      await client.dispose();
    }
  });

  it('reports no project for a folder outside of any project', async () => {
    const client = new ServerClient(serverPath!);
    try {
      await client.ready();
      await client.request({ $type: 'RegisterFileRequest', fileName: path.join(repoRoot, 'Build') });
      const stats = await client.request({ $type: 'GetDatabaseStatisticsRequest' });
      assert.equal(stats.projectCount, 0);
    } finally {
      await client.dispose();
    }
  });
});

describe('ServerClient errors', () => {
  it('rejects ready() and requests when the server cannot be started', async () => {
    const client = new ServerClient(path.join(repoRoot, 'does-not-exist', 'VsChromium.Server.exe'));
    await assert.rejects(client.ready(), ServerExitedError);
    assert.ok(client.hasExited);
    await assert.rejects(client.request({ $type: 'GetDatabaseStatisticsRequest' }), ServerExitedError);
  });

  it('rejects pending requests when the server exits', { skip: serverPath ? false : 'VsChromium.Server.exe not built' }, async () => {
    const client = new ServerClient(serverPath!);
    await client.ready();
    const exited = new Promise<string>(resolve => client.onExit(resolve));
    await client.dispose();
    assert.match(await exited, /exited/);
    await assert.rejects(client.request({ $type: 'GetDatabaseStatisticsRequest' }), ServerExitedError);
  });
});
