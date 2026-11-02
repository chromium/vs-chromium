// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as fs from 'fs';
import * as path from 'path';

export const ServerExeName = 'VsChromium.Server.exe';

/**
 * Returns the candidate locations of VsChromium.Server.exe, in order of preference:
 * 1. The explicit `configuredPath` (setting "vschromium.serverPath"), if not empty.
 * 2. The server bundled with the extension (`<extension>/bin`).
 * 3. During development (extension running from `<repo>/src/vscode`): the server built in
 *    `<repo>/Binaries/Debug` or `<repo>/Binaries/Release`.
 */
export function serverPathCandidates(extensionPath: string, configuredPath: string | undefined): string[] {
  if (configuredPath && configuredPath.trim().length > 0) {
    return [configuredPath.trim()];
  }
  const repoRoot = path.resolve(extensionPath, '..', '..');
  return [
    path.join(extensionPath, 'bin', ServerExeName),
    path.join(repoRoot, 'Binaries', 'Debug', ServerExeName),
    path.join(repoRoot, 'Binaries', 'Release', ServerExeName),
  ];
}

/** Returns the first existing server path, or undefined if none of the candidates exist. */
export function findServerPath(extensionPath: string, configuredPath: string | undefined): string | undefined {
  return serverPathCandidates(extensionPath, configuredPath).find(p => fs.existsSync(p));
}
