// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Copies the indexing server (VsChromium.Server.exe and its dependencies) from
// <repo>/Binaries/<Configuration> into <extension>/bin, for packaging. Also copies the repository
// LICENSE file. Usage: node scripts/copy-server.js [Release|Debug]   (default: Release)

'use strict';

const fs = require('fs');
const path = require('path');

const configuration = process.argv[2] || 'Release';
const extensionRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(extensionRoot, '..', '..');
const sourceDir = path.join(repoRoot, 'Binaries', configuration);
const targetDir = path.join(extensionRoot, 'bin');

// Explicit list, so that packaging fails if a dependency is missing (rather than producing an
// extension that cannot start the server).
const files = [
  'VsChromium.Server.exe',
  'VsChromium.Server.exe.config',
  'VsChromium.Core.dll',
  'VsChromium.Server.NativeInterop.dll',
  'VsChromium.Native.dll',
  'protobuf-net.dll',
  'NLog.dll',
  'Newtonsoft.Json.dll',
];
const directories = ['Configuration'];

function fail(message) {
  console.error(`copy-server: ${message}`);
  process.exit(1);
}

if (!fs.existsSync(sourceDir)) {
  fail(`"${sourceDir}" not found. Build the vs-chromium solution in ${configuration} configuration first.`);
}
const missing = [...files, ...directories].filter(f => !fs.existsSync(path.join(sourceDir, f)));
if (missing.length > 0) {
  fail(`missing in "${sourceDir}": ${missing.join(', ')}`);
}

fs.rmSync(targetDir, { recursive: true, force: true });
fs.mkdirSync(targetDir, { recursive: true });
for (const file of files) {
  fs.copyFileSync(path.join(sourceDir, file), path.join(targetDir, file));
}
for (const dir of directories) {
  fs.cpSync(path.join(sourceDir, dir), path.join(targetDir, dir), { recursive: true });
}
fs.copyFileSync(path.join(repoRoot, 'LICENSE'), path.join(extensionRoot, 'LICENSE'));

console.log(`copy-server: copied ${files.length} files and ${directories.length} directories from "${sourceDir}" to "${targetDir}".`);
