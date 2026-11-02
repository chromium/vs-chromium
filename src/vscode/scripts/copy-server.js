// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Copies the indexing server (VsChromium.Server.exe and its dependencies, built by the vs-chromium
// solution into <repo>/Binaries/<Configuration>/Server) into <extension>/bin, for packaging. Also
// copies the repository LICENSE file. Usage: node scripts/copy-server.js [Release|Debug]
// (default: Release)

'use strict';

const fs = require('fs');
const path = require('path');

const configuration = process.argv[2] || 'Release';
const extensionRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(extensionRoot, '..', '..');
const sourceDir = path.join(repoRoot, 'Binaries', configuration, 'Server');
const targetDir = path.join(extensionRoot, 'bin');

// Files that must be present, so that packaging fails if the server was not built properly (rather
// than producing an extension that cannot start the server). All the files of the server directory
// are copied (except debug symbols).
const requiredFiles = [
  'VsChromium.Server.exe',
  'VsChromium.Server.dll',
  'VsChromium.Server.runtimeconfig.json',
  'VsChromium.Server.deps.json',
  'VsChromium.Core.Server.dll',
  'VsChromium.Core.Ipc.dll',
  'VsChromium.Server.NativeInterop.dll',
  'VsChromium.Native.dll',
  path.join('Configuration', 'SearchableFiles.include'),
];

function fail(message) {
  console.error(`copy-server: ${message}`);
  process.exit(1);
}

if (!fs.existsSync(sourceDir)) {
  fail(`"${sourceDir}" not found. Build the vs-chromium solution in ${configuration} configuration first.`);
}
const missing = requiredFiles.filter(f => !fs.existsSync(path.join(sourceDir, f)));
if (missing.length > 0) {
  fail(`missing in "${sourceDir}": ${missing.join(', ')}`);
}

fs.rmSync(targetDir, { recursive: true, force: true });
fs.cpSync(sourceDir, targetDir, {
  recursive: true,
  filter: source => !source.endsWith('.pdb'),
});
fs.copyFileSync(path.join(repoRoot, 'LICENSE'), path.join(extensionRoot, 'LICENSE'));

const count = fs.readdirSync(targetDir, { recursive: true }).length;
console.log(`copy-server: copied ${count} files from "${sourceDir}" to "${targetDir}".`);
