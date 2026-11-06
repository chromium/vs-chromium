// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Builds the release packages (see docs/release.md), on a developer machine
// (build-release.ps1 / build-release.sh) and in the release workflow
// (.github/workflows/release.yml), into Binaries/Release/Packages/<version>/:
//
//   vs-chromium-<version>-<VS Code target>.vsix   VS Code extension, one per target platform, with
//                                                 the server of that platform
//   vs-chromium-<version>-visualstudio.vsix       Visual Studio extension (x64 and ARM64 Visual
//                                                 Studio, with the win-x64 and win-arm64 servers)
//   vschromium-server-<version>-<rid>.zip         Standalone server
//   vschromium-bench-<version>-<rid>.zip          vschromium-bench
//   SHA256SUMS.txt                                SHA-256 of all the files (sha256sum format)
//
// Usage: node tools/release/release.js <command> [options]
//   check [--tag <tag>]               Version of every file, release notes (and tag).
//   server <rid>...                   Publishes the server, writes its archive.
//   bench <rid>... [--no-aot]         Publishes vschromium-bench (NativeAOT when the runtime
//                                     identifier is of this OS, else single file), writes its archive.
//   vscode <target>... [--no-publish] VS Code extension packages (--no-publish: with the servers
//                                     already published by the "server" command).
//   visualstudio [--no-publish]       Visual Studio extension package (Windows, Visual Studio).
//   verify                            Verifies the packages of the output directory.
//   checksums                         Writes SHA256SUMS.txt (all the files of the output directory).
//   all [--no-aot] [--skip-tests]     Everything that can be built on this machine, then verify and
//                                     checksums. The output directory is emptied first.
// Options: --out <directory> (default: Binaries/Release/Packages/<version>)
//
// Runtime identifiers: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64.

'use strict';

const { spawnSync } = require('child_process');
const crypto = require('crypto');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { writeZip, listZip, readZipEntry, executableKind } = require('./zip');

const repoRoot = path.resolve(__dirname, '..', '..');
const configuration = 'Release';
const binaries = path.join(repoRoot, 'Binaries', configuration);
const vscodeDir = path.join(repoRoot, 'src', 'vscode');

// .NET runtime identifier -> VS Code target platform (vsce package --target). Must match
// src/vscode/scripts/copy-server.js and ReleaseAssets.VsCodeTarget (vschromium-bench).
const platforms = [
  { rid: 'win-x64', target: 'win32-x64' },
  { rid: 'win-arm64', target: 'win32-arm64' },
  { rid: 'linux-x64', target: 'linux-x64' },
  { rid: 'linux-arm64', target: 'linux-arm64' },
  { rid: 'osx-x64', target: 'darwin-x64' },
  { rid: 'osx-arm64', target: 'darwin-arm64' },
];
const visualStudioRids = ['win-x64', 'win-arm64'];

const hostOs = process.platform === 'win32' ? 'win' : process.platform === 'darwin' ? 'osx' : 'linux';
const hostArch = os.arch() === 'arm64' ? 'arm64' : 'x64';
const hostRid = `${hostOs}-${hostArch}`;
const onCi = !!process.env.GITHUB_ACTIONS;

// Notes printed in the summary (unsigned binaries, NativeAOT fallbacks...).
const notes = [];

function fail(message) {
  console.error(`release: ERROR: ${message}`);
  process.exit(1);
}

function warn(message) {
  notes.push(message);
  console.log(onCi ? `::warning::${message}` : `release: WARNING: ${message}`);
}

function log(message) {
  console.log(`release: ${message}`);
}

function run(command, args, options = {}) {
  log(`> ${command} ${args.join(' ')}`);
  // npm is a batch file on Windows: it needs a shell (the arguments have no spaces).
  const shell = process.platform === 'win32' && /^(npm|npx)$/.test(command);
  const result = shell ?
    spawnSync([command, ...args].join(' '), { stdio: 'inherit', shell, ...options }) :
    spawnSync(command, args, { stdio: 'inherit', ...options });
  if (options.allowFailure) {
    return result.status === 0;
  }
  if (result.error) {
    fail(`${command}: ${result.error.message}`);
  }
  if (result.status !== 0) {
    fail(`${command} ${args.join(' ')}: exit code ${result.status}`);
  }
  return true;
}

function readVersion() {
  const version = fs.readFileSync(path.join(repoRoot, 'VERSION'), 'utf8').trim();
  if (!/^\d+\.\d+\.\d+$/.test(version)) {
    fail(`invalid version in VERSION: "${version}"`);
  }
  return version;
}

function platformOfRid(rid) {
  const platform = platforms.find(p => p.rid === rid);
  if (!platform) {
    fail(`unknown runtime identifier "${rid}" (expected one of: ${platforms.map(p => p.rid).join(', ')})`);
  }
  return platform;
}

function platformOfTarget(target) {
  const platform = platforms.find(p => p.target === target);
  if (!platform) {
    fail(`unknown VS Code target "${target}" (expected one of: ${platforms.map(p => p.target).join(', ')})`);
  }
  return platform;
}

function executableName(rid, baseName) {
  return rid.startsWith('win-') ? baseName + '.exe' : baseName;
}

function listFiles(directory, filter = () => true) {
  const result = [];
  for (const entry of fs.readdirSync(directory, { withFileTypes: true, recursive: true })) {
    if (entry.isFile()) {
      const full = path.join(entry.parentPath, entry.name);
      if (filter(full)) {
        result.push(full);
      }
    }
  }
  return result.sort();
}

function sha256(file) {
  return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
}

function megabytes(bytes) {
  return (bytes / 1048576).toFixed(1) + ' MB';
}

/**
 * macOS executables must be code signed to run on Apple Silicon (at least ad-hoc): the .NET SDK
 * signs them when publishing on macOS; signs them ad-hoc if not, and verifies. They can't be
 * signed on other OSes.
 */
function signForMacOs(file, rid) {
  if (!rid.startsWith('osx-')) {
    return;
  }
  if (hostOs !== 'osx') {
    warn(`${path.basename(file)} (${rid}) is NOT code signed (built on ${hostOs}): macOS on Apple Silicon refuses to run it. Build the macOS packages on macOS (release workflow).`);
    return;
  }
  if (!run('codesign', ['--verify', file], { allowFailure: true })) {
    run('codesign', ['--force', '--sign', '-', file]);
  }
  run('codesign', ['--verify', '--verbose=2', file]);
}

/** Runs an executable of this platform with a usage argument, checks its output. */
function smokeTest(file, rid, args, expected) {
  if (rid !== hostRid) {
    return;
  }
  const result = spawnSync(file, args, { encoding: 'utf8', timeout: 60000 });
  const output = `${result.stdout || ''}${result.stderr || ''}`;
  if (result.error || !output.includes(expected)) {
    fail(`smoke test of ${file} ${args.join(' ')}: expected "${expected}" in the output (${result.error ? result.error.message : `exit code ${result.status}`}):\n${output}`);
  }
  log(`smoke test OK: ${path.basename(file)} ${args.join(' ')}`);
}

function checkExecutableKind(buffer, rid, what) {
  const kind = executableKind(buffer);
  const [ridOs, ridArch] = rid.split('-');
  if (!kind || kind.os !== ridOs || kind.arch !== ridArch) {
    fail(`${what}: not a ${rid} executable (${kind ? `${kind.os}-${kind.arch}` : 'unknown format'})`);
  }
}

function zipDirectory(zipPath, directory, prefix, executables) {
  const entries = listFiles(directory).map(file => {
    const relative = path.relative(directory, file).split(path.sep).join('/');
    let mode = 0o644;
    if (executables.includes(relative) || (hostOs !== 'win' && (fs.statSync(file).mode & 0o111) !== 0)) {
      mode = 0o755;
    }
    return { name: `${prefix}/${relative}`, file, mode };
  });
  fs.rmSync(zipPath, { force: true });
  writeZip(zipPath, entries);
  log(`${path.basename(zipPath)}: ${entries.length} files, ${megabytes(fs.statSync(zipPath).size)}`);
}

// --- Commands ---

function commandCheck(options) {
  const args = ['--check', '--notes'];
  if (options.tag) {
    args.push('--tag', options.tag);
  }
  run(process.execPath, [path.join(__dirname, 'set-version.js'), ...args]);
}

function serverPublishDir(rid) {
  return path.join(binaries, 'ServerPublish', rid);
}

function commandServer(rids, options) {
  for (const rid of rids) {
    platformOfRid(rid);
    const publishDir = serverPublishDir(rid);
    fs.rmSync(publishDir, { recursive: true, force: true });
    run('dotnet', ['publish', path.join(repoRoot, 'src', 'Server', 'Server.csproj'), '-c', configuration, '-r', rid, '--nologo', '-v:quiet']);
    const exe = path.join(publishDir, executableName(rid, 'VsChromium.Server'));
    if (!fs.existsSync(exe)) {
      fail(`${exe} not found after dotnet publish`);
    }
    checkExecutableKind(fs.readFileSync(exe), rid, exe);
    signForMacOs(exe, rid);
    smokeTest(exe, rid, ['--help'], '--jsonrpc');

    // Archive: the published files (no debug symbols) and the license.
    const staging = path.join(binaries, 'ReleaseStaging', `vschromium-server-${rid}`);
    fs.rmSync(staging, { recursive: true, force: true });
    fs.cpSync(publishDir, staging, { recursive: true, filter: source => !source.endsWith('.pdb') });
    fs.copyFileSync(path.join(repoRoot, 'LICENSE'), path.join(staging, 'LICENSE'));
    const name = `vschromium-server-${options.version}-${rid}`;
    zipDirectory(path.join(options.out, name + '.zip'), staging, name, [path.basename(exe)]);
  }
}

function commandBench(rids, options) {
  for (const rid of rids) {
    platformOfRid(rid);
    const publishDir = path.join(binaries, 'BenchPublish', rid);
    const project = path.join(repoRoot, 'src', 'Tools', 'Bench', 'Bench.csproj');
    let mode = 'single-file';
    fs.rmSync(publishDir, { recursive: true, force: true });
    // NativeAOT can't cross-compile to another OS (and needs the native tools of the target).
    if (!options.noAot && rid.split('-')[0] === hostOs) {
      if (run('dotnet', ['publish', project, '-c', configuration, '-r', rid, '-p:BenchAot=true', '-o', publishDir, '--nologo', '-v:quiet'], { allowFailure: true })) {
        mode = 'NativeAOT';
      } else {
        warn(`vschromium-bench ${rid}: NativeAOT publish failed, falling back to single-file.`);
        fs.rmSync(publishDir, { recursive: true, force: true });
      }
    }
    if (mode !== 'NativeAOT') {
      run('dotnet', ['publish', project, '-c', configuration, '-r', rid, '-p:BenchAot=false', '-o', publishDir, '--nologo', '-v:quiet']);
    }
    const exeName = executableName(rid, 'vschromium-bench');
    const exe = path.join(publishDir, exeName);
    if (!fs.existsSync(exe)) {
      fail(`${exe} not found after dotnet publish`);
    }
    checkExecutableKind(fs.readFileSync(exe), rid, exe);
    signForMacOs(exe, rid);
    smokeTest(exe, rid, ['-h'], 'vschromium-bench');

    const staging = path.join(binaries, 'ReleaseStaging', `vschromium-bench-${rid}`);
    fs.rmSync(staging, { recursive: true, force: true });
    fs.mkdirSync(staging, { recursive: true });
    fs.copyFileSync(exe, path.join(staging, exeName));
    fs.copyFileSync(path.join(repoRoot, 'src', 'Tools', 'Bench', 'README.md'), path.join(staging, 'README.md'));
    fs.copyFileSync(path.join(repoRoot, 'LICENSE'), path.join(staging, 'LICENSE'));
    const name = `vschromium-bench-${options.version}-${rid}`;
    zipDirectory(path.join(options.out, name + '.zip'), staging, name, [exeName]);
    log(`vschromium-bench ${rid}: ${mode}`);
    notes.push(`vschromium-bench ${rid}: ${mode}`);
  }
}

function ensureNodeModules() {
  if (fs.existsSync(path.join(vscodeDir, 'node_modules', '@vscode', 'vsce'))) {
    return;
  }
  // package-lock.json is not committed (see src/vscode/.gitignore).
  const lock = fs.existsSync(path.join(vscodeDir, 'package-lock.json'));
  run('npm', [lock ? 'ci' : 'install', '--no-audit', '--no-fund'], { cwd: vscodeDir });
}

function vsceCommand() {
  const packageJson = require.resolve('@vscode/vsce/package.json', { paths: [vscodeDir] });
  const bin = JSON.parse(fs.readFileSync(packageJson, 'utf8')).bin;
  return path.join(path.dirname(packageJson), typeof bin === 'string' ? bin : bin.vsce);
}

function commandVsCode(targets, options) {
  ensureNodeModules();
  for (const target of targets) {
    const { rid } = platformOfTarget(target);
    if (options.noPublish && !fs.existsSync(serverPublishDir(rid))) {
      fail(`${serverPublishDir(rid)} not found: run "release.js server ${rid}" first (or omit --no-publish).`);
    }
    if (!options.noPublish) {
      commandServer([rid], options);
    }
    run(process.execPath, [path.join(vscodeDir, 'scripts', 'copy-server.js'), configuration, target, '--no-publish']);
    const vsix = path.join(options.out, `vs-chromium-${options.version}-${target}.vsix`);
    fs.rmSync(vsix, { force: true });
    run(process.execPath, [vsceCommand(), 'package', '--target', target, '--out', vsix], { cwd: vscodeDir });
    log(`${path.basename(vsix)}: ${megabytes(fs.statSync(vsix).size)}`);
  }
}

function findMsBuild() {
  const vswhere = path.join(process.env['ProgramFiles(x86)'] || 'C:\\Program Files (x86)', 'Microsoft Visual Studio', 'Installer', 'vswhere.exe');
  if (!fs.existsSync(vswhere)) {
    fail(`vswhere not found (${vswhere}): Visual Studio (with the "Visual Studio extension development" workload) is required.`);
  }
  const result = spawnSync(vswhere, ['-latest', '-prerelease', '-products', '*', '-requires', 'Microsoft.Component.MSBuild', '-find', 'MSBuild\\**\\Bin\\MSBuild.exe'], { encoding: 'utf8' });
  const msbuild = (result.stdout || '').split(/\r?\n/).find(line => line.trim().length > 0);
  if (!msbuild) {
    fail('MSBuild not found by vswhere.');
  }
  return msbuild.trim();
}

function commandVisualStudio(options) {
  if (hostOs !== 'win') {
    fail('the Visual Studio extension can only be built on Windows.');
  }
  if (options.noPublish) {
    for (const rid of visualStudioRids) {
      if (!fs.existsSync(path.join(serverPublishDir(rid), 'VsChromium.Server.exe'))) {
        fail(`${serverPublishDir(rid)} not found: run "release.js server ${rid}" first (or omit --no-publish).`);
      }
    }
  }
  ensureNodeModules(); // For the dashboard page (BuildDashboardPage target).
  const vsixOutput = path.join(binaries, 'VsChromium.vsix');
  fs.rmSync(vsixOutput, { force: true });
  run(findMsBuild(), [
    path.join(repoRoot, 'src', 'VsChromium', 'VsChromium.csproj'),
    '-restore', `-p:Configuration=${configuration}`, `-p:VsChromiumPublishServer=${options.noPublish ? 'false' : 'true'}`,
    '-m', '-v:minimal', '-nologo',
  ]);
  if (!fs.existsSync(vsixOutput)) {
    fail(`${vsixOutput} not found after the build.`);
  }
  const vsix = path.join(options.out, `vs-chromium-${options.version}-visualstudio.vsix`);
  fs.copyFileSync(vsixOutput, vsix);
  log(`${path.basename(vsix)}: ${megabytes(fs.statSync(vsix).size)}`);
}

// --- Verification of the packages ---

function verifyVsCodeVsix(file, target, version) {
  const { rid } = platformOfTarget(target);
  const manifest = (readZipEntry(file, 'extension.vsixmanifest') || '').toString('utf8');
  if (!manifest.includes(`TargetPlatform="${target}"`) || !manifest.includes(`Version="${version}"`)) {
    fail(`${path.basename(file)}: the manifest doesn't have TargetPlatform="${target}" and Version="${version}".`);
  }
  const exeName = `extension/bin/${executableName(rid, 'VsChromium.Server')}`;
  const exe = readZipEntry(file, exeName);
  if (!exe) {
    fail(`${path.basename(file)}: ${exeName} missing.`);
  }
  checkExecutableKind(exe, rid, `${path.basename(file)}: ${exeName}`);
  for (const required of ['extension/bin/Configuration/SearchableFiles.include', 'extension/out/extension.js', 'extension/package.json']) {
    if (!listZip(file).some(e => e.name === required)) {
      fail(`${path.basename(file)}: ${required} missing.`);
    }
  }
}

function verifyVisualStudioVsix(file, version) {
  const manifest = (readZipEntry(file, 'extension.vsixmanifest') || '').toString('utf8');
  for (const expected of [`Version="${version}"`, '<ProductArchitecture>amd64</ProductArchitecture>', '<ProductArchitecture>arm64</ProductArchitecture>']) {
    if (!manifest.includes(expected)) {
      fail(`${path.basename(file)}: the manifest doesn't contain ${expected}.`);
    }
  }
  const names = listZip(file).map(e => e.name);
  for (const rid of visualStudioRids) {
    const exeName = `Server/${rid}/VsChromium.Server.exe`;
    const exe = readZipEntry(file, exeName);
    if (!exe) {
      fail(`${path.basename(file)}: ${exeName} missing.`);
    }
    checkExecutableKind(exe, rid, `${path.basename(file)}: ${exeName}`);
    if (!names.includes(`Server/${rid}/Configuration/SearchableFiles.include`)) {
      fail(`${path.basename(file)}: Server/${rid}/Configuration/SearchableFiles.include missing.`);
    }
  }
  for (const required of ['VsChromium.dll', 'Dashboard/dashboard.js', 'Dashboard/dashboard.html']) {
    if (!names.includes(required)) {
      fail(`${path.basename(file)}: ${required} missing.`);
    }
  }
}

function verifyArchive(file, prefix, rid, baseName) {
  const exeName = `${prefix}/${executableName(rid, baseName)}`;
  const entry = listZip(file).find(e => e.name === exeName);
  if (!entry) {
    fail(`${path.basename(file)}: ${exeName} missing.`);
  }
  if (entry.mode !== 0o755) {
    fail(`${path.basename(file)}: ${exeName} is not executable (mode ${entry.mode.toString(8)}).`);
  }
  checkExecutableKind(readZipEntry(file, exeName), rid, `${path.basename(file)}: ${exeName}`);
}

function commandVerify(options) {
  const files = fs.existsSync(options.out) ? fs.readdirSync(options.out).filter(f => f !== 'SHA256SUMS.txt').sort() : [];
  if (files.length === 0) {
    fail(`no packages in ${options.out}`);
  }
  const v = options.version.replace(/\./g, '\\.');
  for (const name of files) {
    const file = path.join(options.out, name);
    let match;
    if ((match = new RegExp(`^vs-chromium-${v}-visualstudio\\.vsix$`).exec(name))) {
      verifyVisualStudioVsix(file, options.version);
    } else if ((match = new RegExp(`^vs-chromium-${v}-([a-z0-9]+-[a-z0-9]+)\\.vsix$`).exec(name))) {
      verifyVsCodeVsix(file, match[1], options.version);
    } else if ((match = new RegExp(`^(vschromium-server-${v}-([a-z]+-[a-z0-9]+))\\.zip$`).exec(name))) {
      verifyArchive(file, match[1], platformOfRid(match[2]).rid, 'VsChromium.Server');
    } else if ((match = new RegExp(`^(vschromium-bench-${v}-([a-z]+-[a-z0-9]+))\\.zip$`).exec(name))) {
      verifyArchive(file, match[1], platformOfRid(match[2]).rid, 'vschromium-bench');
    } else {
      fail(`unexpected file in ${options.out}: ${name}`);
    }
    log(`verified ${name}`);
  }
  if (options.complete) {
    const expected = [
      ...platforms.map(p => `vs-chromium-${options.version}-${p.target}.vsix`),
      `vs-chromium-${options.version}-visualstudio.vsix`,
      ...platforms.map(p => `vschromium-server-${options.version}-${p.rid}.zip`),
      ...platforms.map(p => `vschromium-bench-${options.version}-${p.rid}.zip`),
    ];
    const missing = expected.filter(f => !files.includes(f));
    if (missing.length > 0) {
      fail(`missing packages: ${missing.join(', ')}`);
    }
  }
}

function commandChecksums(options) {
  const files = fs.readdirSync(options.out).filter(f => f !== 'SHA256SUMS.txt' && fs.statSync(path.join(options.out, f)).isFile()).sort();
  const lines = files.map(f => `${sha256(path.join(options.out, f))}  ${f}`);
  fs.writeFileSync(path.join(options.out, 'SHA256SUMS.txt'), lines.join('\n') + '\n', 'utf8');
  log(`SHA256SUMS.txt: ${files.length} files`);
}

function printSummary(options) {
  console.log('');
  console.log(`Packages of VsChromium ${options.version} in ${options.out}:`);
  for (const f of fs.readdirSync(options.out).sort()) {
    const size = fs.statSync(path.join(options.out, f)).size;
    console.log(`  ${f.padEnd(48)} ${megabytes(size).padStart(10)}`);
  }
  if (notes.length > 0) {
    console.log('');
    console.log('Notes:');
    for (const note of notes) {
      console.log(`  - ${note}`);
    }
  }
}

function commandAll(options) {
  commandCheck(options);
  fs.rmSync(options.out, { recursive: true, force: true });
  fs.mkdirSync(options.out, { recursive: true });
  const rids = platforms.map(p => p.rid);
  commandServer(rids, options);
  commandBench(rids, options);
  commandVsCode(platforms.map(p => p.target), { ...options, noPublish: true });
  if (hostOs === 'win') {
    commandVisualStudio({ ...options, noPublish: true });
  } else {
    warn('the Visual Studio extension is built on Windows only: not built.');
  }
  commandVerify({ ...options, complete: hostOs === 'win' });
  commandChecksums(options);
  printSummary(options);
}

function main(argv) {
  const command = argv[0];
  const positional = [];
  const options = { version: readVersion(), noAot: false, noPublish: false };
  for (let i = 1; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--out' && i + 1 < argv.length) {
      options.out = path.resolve(argv[++i]);
    } else if (arg === '--tag' && i + 1 < argv.length) {
      options.tag = argv[++i];
    } else if (arg === '--no-aot') {
      options.noAot = true;
    } else if (arg === '--no-publish') {
      options.noPublish = true;
    } else if (arg === '--complete') {
      options.complete = true;
    } else if (arg.startsWith('--')) {
      fail(`unknown option "${arg}"`);
    } else {
      positional.push(arg);
    }
  }
  options.out = options.out || path.join(binaries, 'Packages', options.version);
  if (command !== 'check') {
    fs.mkdirSync(options.out, { recursive: true });
  }
  switch (command) {
    case 'check':
      commandCheck(options);
      break;
    case 'server':
      commandServer(positional, options);
      break;
    case 'bench':
      commandBench(positional, options);
      break;
    case 'vscode':
      commandVsCode(positional, options);
      break;
    case 'visualstudio':
      commandVisualStudio(options);
      break;
    case 'verify':
      commandVerify(options);
      break;
    case 'checksums':
      commandChecksums(options);
      printSummary(options);
      break;
    case 'all':
      commandAll(options);
      break;
    default:
      fail('usage: release.js check|server|bench|vscode|visualstudio|verify|checksums|all [options] (see the comments of tools/release/release.js)');
  }
  if (command !== 'all' && command !== 'checksums' && notes.length > 0) {
    console.log('');
    for (const note of notes) {
      console.log(`note: ${note}`);
    }
  }
}

main(process.argv.slice(2));
