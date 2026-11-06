// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Publishes the indexing server as a self-contained, single file, ReadyToRun executable for the
// given VS Code target platform (`dotnet publish`, output in
// <repo>/Binaries/<Configuration>/ServerPublish/<runtime identifier>, see src/Server/Server.csproj)
// and copies it into <extension>/bin, for packaging: users don't need to install a .NET runtime.
// Also copies the repository LICENSE file.
// Usage: node scripts/copy-server.js [Release|Debug] [<VS Code target>] [--no-publish]
// (defaults: Release, win32-x64; targets: win32-x64, win32-arm64, linux-x64, linux-arm64,
// darwin-x64, darwin-arm64). --no-publish: copies the server already published in
// Binaries/<Configuration>/ServerPublish/<runtime identifier> (release builds, see docs/release.md).

'use strict';

const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const args = process.argv.slice(2).filter(a => !a.startsWith('--'));
const noPublish = process.argv.includes('--no-publish');
const configuration = args[0] || 'Release';
const extensionRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(extensionRoot, '..', '..');
const target = args[1] || 'win32-x64';
// VS Code target platform -> .NET runtime identifier
const runtimeIdentifiers = {
  'win32-x64': 'win-x64',
  'win32-arm64': 'win-arm64',
  'linux-x64': 'linux-x64',
  'linux-arm64': 'linux-arm64',
  'darwin-x64': 'osx-x64',
  'darwin-arm64': 'osx-arm64',
};
const runtimeIdentifier = runtimeIdentifiers[target];
const serverExeName = target.startsWith('win32-') ? 'VsChromium.Server.exe' : 'VsChromium.Server';
const sourceDir = path.join(repoRoot, 'Binaries', configuration, 'ServerPublish', runtimeIdentifier || target);
const targetDir = path.join(extensionRoot, 'bin');

// Files that must be present, so that packaging fails if the server was not built properly (rather
// than producing an extension that cannot start the server). All the files of the server directory
// are copied (except debug symbols).
const requiredFiles = [
  serverExeName,
  path.join('Configuration', 'SearchableFiles.include'),
];

function fail(message) {
  console.error(`copy-server: ${message}`);
  process.exit(1);
}

if (!runtimeIdentifier) {
  fail(`unknown target "${target}" (expected one of: ${Object.keys(runtimeIdentifiers).join(', ')})`);
}

const serverProject = path.join(repoRoot, 'src', 'Server', 'Server.csproj');
if (!noPublish) {
  console.log(`copy-server: publishing ${serverProject} (${configuration}, ${runtimeIdentifier})...`);
  try {
    execFileSync('dotnet', ['publish', serverProject, '-c', configuration, '-r', runtimeIdentifier, '--nologo', '-v:quiet'],
      { stdio: 'inherit' });
  } catch (e) {
    fail(`dotnet publish failed: ${e.message}`);
  }
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
