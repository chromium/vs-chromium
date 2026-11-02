// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Publishes the indexing server as a self-contained, single file, ReadyToRun executable for win-x64
// (`dotnet publish`, output in <repo>/Binaries/<Configuration>/ServerPublish/win-x64, see
// src/Server/Server.csproj) and copies it into <extension>/bin, for packaging: users don't need to
// install a .NET runtime. Also copies the repository LICENSE file.
// Usage: node scripts/copy-server.js [Release|Debug] (default: Release)

'use strict';

const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const configuration = process.argv[2] || 'Release';
const extensionRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(extensionRoot, '..', '..');
const runtimeIdentifier = 'win-x64';
const sourceDir = path.join(repoRoot, 'Binaries', configuration, 'ServerPublish', runtimeIdentifier);
const targetDir = path.join(extensionRoot, 'bin');

// Files that must be present, so that packaging fails if the server was not built properly (rather
// than producing an extension that cannot start the server). All the files of the server directory
// are copied (except debug symbols).
const requiredFiles = [
  'VsChromium.Server.exe',
  path.join('Configuration', 'SearchableFiles.include'),
];

function fail(message) {
  console.error(`copy-server: ${message}`);
  process.exit(1);
}

const serverProject = path.join(repoRoot, 'src', 'Server', 'Server.csproj');
console.log(`copy-server: publishing ${serverProject} (${configuration}, ${runtimeIdentifier})...`);
try {
  execFileSync('dotnet', ['publish', serverProject, '-c', configuration, '-r', runtimeIdentifier, '--nologo', '-v:quiet'],
    { stdio: 'inherit' });
} catch (e) {
  fail(`dotnet publish failed: ${e.message}`);
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
