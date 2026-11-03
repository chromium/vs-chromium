// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import { afterEach, describe, it } from 'node:test';
import { findServerPath, ServerExeName } from '../server/serverLocator';

describe('serverLocator', () => {
  const tempDirs: string[] = [];

  function makeRepoLayout() {
    const repoRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'vschromium-locator-'));
    tempDirs.push(repoRoot);
    const extensionPath = path.join(repoRoot, 'src', 'vscode');
    const bundledExe = path.join(extensionPath, 'bin', ServerExeName);
    const debugExe = path.join(repoRoot, 'Binaries', 'Debug', 'Server', ServerExeName);
    const releaseExe = path.join(repoRoot, 'Binaries', 'Release', 'Server', ServerExeName);
    fs.mkdirSync(extensionPath, { recursive: true });
    return { extensionPath, bundledExe, debugExe, releaseExe };
  }

  function touchFile(filePath: string, mtimeSecondsAgo: number) {
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.writeFileSync(filePath, '');
    const time = new Date(Date.now() - mtimeSecondsAgo * 1000);
    fs.utimesSync(filePath, time, time);
  }

  afterEach(() => {
    for (const dir of tempDirs.splice(0)) {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });

  it('prefers the newest repository build over a stale Debug or bundled binary in development mode', () => {
    const { extensionPath, bundledExe, debugExe, releaseExe } = makeRepoLayout();
    touchFile(bundledExe, 10);
    touchFile(debugExe, 3600);
    touchFile(releaseExe, 60);

    assert.equal(findServerPath(extensionPath, undefined, /*preferRepositoryBuild*/ true), releaseExe);

    touchFile(debugExe, 5);
    assert.equal(findServerPath(extensionPath, undefined, /*preferRepositoryBuild*/ true), debugExe);
  });

  it('prefers the bundled binary when not in development mode', () => {
    const { extensionPath, bundledExe, releaseExe } = makeRepoLayout();
    touchFile(bundledExe, 3600);
    touchFile(releaseExe, 5);

    assert.equal(findServerPath(extensionPath, undefined, /*preferRepositoryBuild*/ false), bundledExe);
  });

  it('uses an explicit configured path when set', () => {
    const { extensionPath, debugExe } = makeRepoLayout();
    touchFile(debugExe, 5);

    assert.equal(findServerPath(extensionPath, `  ${debugExe}  `, /*preferRepositoryBuild*/ false), debugExe);
  });
});
