// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as fs from 'fs';
import * as path from 'path';

/** The name of the server executable (no extension outside of Windows). */
export const ServerExeName = process.platform === 'win32' ? 'VsChromium.Server.exe' : 'VsChromium.Server';

/**
 * Returns the candidate locations of VsChromium.Server.exe, in order of preference:
 * 1. The explicit `configuredPath` (setting "vschromium.serverPath"), if not empty.
 * 2. The server bundled with the extension (`<extension>/bin`), copied there when packaging.
 * 3. When running from the repository (`<repo>/src/vscode`): the server built in
 *    `<repo>/Binaries/Release/Server` (built by the "build" task of the extension), or else in
 *    `<repo>/Binaries/Debug/Server`. The Release build is preferred even when the Debug build is
 *    newer: the Debug build is about 2x slower on some searches (JIT optimizations disabled). To
 *    run a Debug build of the server, set "vschromium.serverPath".
 *
 * When `preferRepositoryBuild` is true (extension running in development mode), 3 comes before 2
 * so that a stale `bin` directory left over from packaging does not shadow the latest build.
 */
export function serverPathCandidates(extensionPath: string, configuredPath: string | undefined, preferRepositoryBuild = false): string[] {
  if (configuredPath && configuredPath.trim().length > 0) {
    return [configuredPath.trim()];
  }
  const repoRoot = path.resolve(extensionPath, '..', '..');
  const bundled = [path.join(extensionPath, 'bin', ServerExeName)];
  const repositoryBuild = [
    path.join(repoRoot, 'Binaries', 'Release', 'Server', ServerExeName),
    path.join(repoRoot, 'Binaries', 'Debug', 'Server', ServerExeName),
  ];
  return preferRepositoryBuild ? [...repositoryBuild, ...bundled] : [...bundled, ...repositoryBuild];
}

/** Returns the first existing server path, or undefined if none of the candidates exist. */
export function findServerPath(extensionPath: string, configuredPath: string | undefined, preferRepositoryBuild = false): string | undefined {
  return serverPathCandidates(extensionPath, configuredPath, preferRepositoryBuild).find(p => fs.existsSync(p));
}
