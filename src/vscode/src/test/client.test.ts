// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Integration tests of ServerClient against the real VsChromium.Server.exe ("--jsonrpc"), built
// in <repo>/Binaries/Release (or Debug) (skipped if the server has not been built). The variable
// VSCHROMIUM_SERVER_PATH overrides the location of the server.

import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { after, before, describe, it } from 'node:test';
import {
  FilesExtractsRequest,
  FilesRegisterRequest,
  IndexStatisticsRequest,
  SearchCodeRequest,
  SearchPathsRequest,
} from '../protocol/protocol.g';
import { ErrorCodes, ServerClient, ServerError, ServerExitedError, userErrorMessage } from '../server/client';
import { findServerPath, ServerExeName } from '../server/serverLocator';
import { flattenFilePaths, flattenSearchResults, toDisplayLine } from '../ui/codeSearchModel';
import { parseFilePathQuery } from '../ui/filePathQuery';
import { IndexEvent, subscribeIndexEvents } from '../ui/indexingState';

const extensionRoot = path.resolve(__dirname, '..', '..');
const repoRoot = path.resolve(extensionRoot, '..', '..');
const serverPath = process.env.VSCHROMIUM_SERVER_PATH ||
  findServerPath(extensionRoot, undefined, /*preferRepositoryBuild*/ true);
const testEnlistmentFile = path.join(repoRoot, 'src', 'Tests', 'TestData', 'chromium', 'src', 'PRESUBMIT.py');
const skip = serverPath ? false : 'VsChromium.Server.exe not built';

function startServer(logs?: string[]): ServerClient {
  return ServerClient.start(serverPath!, { clientName: 'VS Code tests', clientVersion: '0.0.0', log: m => logs?.push(m) });
}

function waitForEvent(client: ServerClient, type: IndexEvent['type'], timeoutMs = 10000): Promise<IndexEvent> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      unsubscribe();
      reject(new Error(`Timeout waiting for notification ${type}`));
    }, timeoutMs);
    const unsubscribe = subscribeIndexEvents(client, event => {
      if (event.type === type) {
        clearTimeout(timer);
        unsubscribe();
        resolve(event);
      }
    });
  });
}

describe('ServerClient', { skip }, () => {
  let client: ServerClient;
  const logs: string[] = [];

  before(async () => {
    client = startServer(logs);
    await client.ready();
  });

  after(async () => {
    await client.dispose();
  });

  it('initializes the connection (server/initialize)', async () => {
    const result = await client.ready();
    assert.equal(result.protocolVersion, 1);
    assert.equal(result.serverProcessId, client.pid);
    assert.ok(result.serverVersion.length > 0);
  });

  it('registers a project and reports indexing notifications and statistics', async () => {
    assert.ok(fs.existsSync(testEnlistmentFile), `Missing test data: ${testEnlistmentFile}`);
    const loaded = waitForEvent(client, 'loaded');

    assert.equal(await client.request(FilesRegisterRequest.type, { path: testEnlistmentFile }), null);
    const event = await loaded;
    assert.equal(event.type === 'loaded' && event.params.error, undefined);

    const stats = await client.request(IndexStatisticsRequest.type, {});
    assert.equal(stats.projectCount, 1);
    assert.ok(stats.fileCount > 0, 'fileCount should be > 0');
    assert.ok(['idle', 'paused', 'yield', 'busy', 'verifying'].includes(stats.status));
  });

  it('searches code and returns text extracts for the matches', async () => {
    const response = await client.request(SearchCodeRequest.type, {
      query: { text: 'looking like', matchCase: true },
      maxResults: 100,
    });
    assert.equal(response.hitCount, 1);

    const files = flattenSearchResults(response);
    assert.equal(files.length, 1);
    assert.equal(files[0].fullPath, path.join(path.dirname(testEnlistmentFile), 'readme.txt'));
    assert.equal(files[0].relativePath, 'readme.txt');

    const extracts = await client.request(FilesExtractsRequest.type, {
      path: files[0].fullPath,
      spans: files[0].spans,
      maxExtractLength: 120,
    });
    assert.equal(extracts.extracts.length, 1);
    const line = toDisplayLine(extracts.extracts[0], files[0].spans[0]);
    assert.deepEqual(line, {
      lineNumber: 1,
      columnNumber: 16,
      before: 'Test directory ',
      match: 'looking like',
      after: ' a local Chromium enlistment.',
    });
  });

  it('returns an empty result for a search without matches', async () => {
    const response = await client.request(SearchCodeRequest.type, {
      query: { text: 'this text does not exist anywhere 1234' },
      maxResults: 100,
    });
    assert.equal(response.hitCount, 0);
    assert.deepEqual(flattenSearchResults(response), []);
  });

  it('reports invalid regular expressions as recoverable errors (-32001)', async () => {
    const error = await client.request(SearchCodeRequest.type, { query: { text: 'foo(', regex: true }, maxResults: 100 })
      .then(() => assert.fail('The search should fail'), (e: unknown) => e);
    assert.ok(error instanceof ServerError, String(error));
    assert.equal(error.code, ErrorCodes.recoverableError);
    assert.equal(userErrorMessage(error), error.message);
    assert.ok(error.message.length > 0);
  });

  it('searches file paths, including paths pasted from build output', async () => {
    const search = async (text: string) => {
      const query = parseFilePathQuery(text);
      const response = await client.request(SearchPathsRequest.type, { query: { text: query.pattern }, maxResults: 100 });
      assert.ok(response.totalCount > 0, 'totalCount should be > 0');
      return flattenFilePaths(response).map(f => f.relativePath).sort();
    };

    assert.deepEqual(await search('readme.txt'), ['readme.txt']);
    assert.ok((await search('*.txt')).includes('readme2.txt'));
    assert.deepEqual(await search('../../readme.txt:1:5: error: foo'), ['readme.txt']);
    assert.deepEqual(await search('does_not_exist_anywhere.xyz'), []);
  });

  it('runs concurrent requests', async () => {
    const results = await Promise.all([1, 2, 3, 4, 5].map(() => client.request(IndexStatisticsRequest.type, {})));
    for (const r of results) {
      assert.equal(r.projectCount, 1);
    }
  });
});

describe('ServerClient workspace folder registration', { skip }, () => {
  it('discovers the project when registering a folder (as done for VS Code workspace folders)', async () => {
    const client = startServer();
    try {
      await client.ready();
      const loaded = waitForEvent(client, 'loaded');
      await client.request(FilesRegisterRequest.type, { path: path.dirname(testEnlistmentFile) });
      await loaded;
      const stats = await client.request(IndexStatisticsRequest.type, {});
      assert.equal(stats.projectCount, 1);
    } finally {
      await client.dispose();
    }
  });

  it('reports no project for a folder outside of any project', async () => {
    const client = startServer();
    try {
      await client.ready();
      await client.request(FilesRegisterRequest.type, { path: path.join(repoRoot, 'Build') });
      const stats = await client.request(IndexStatisticsRequest.type, {});
      assert.equal(stats.projectCount, 0);
    } finally {
      await client.dispose();
    }
  });
});

describe('ServerClient errors', () => {
  it('rejects ready() and requests when the server cannot be started', async () => {
    const client = ServerClient.start(path.join(repoRoot, 'does-not-exist', ServerExeName));
    await assert.rejects(client.ready(), ServerExitedError);
    assert.ok(client.hasExited);
    await assert.rejects(client.request(IndexStatisticsRequest.type, {}), ServerExitedError);
  });

  it('rejects requests after the server exits', { skip }, async () => {
    const client = startServer();
    await client.ready();
    const exited = new Promise<string>(resolve => client.onExit(resolve));
    await client.dispose();
    assert.match(await exited, /exited/);
    await assert.rejects(client.request(IndexStatisticsRequest.type, {}), ServerExitedError);
  });
});
