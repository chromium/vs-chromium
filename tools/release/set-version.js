// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// The product version: the VERSION file at the root of the repository is the reference, copied in
// every file that contains the version (see `targets` below). See docs/release.md.
//
// Usage:
//   node tools/release/set-version.js <major.minor.patch>   Writes VERSION and updates every file.
//   node tools/release/set-version.js --check [--tag <tag>] [--notes]
//       Checks that every file has the version of VERSION (exit code 1 otherwise); with --tag, that
//       the tag (e.g. "v0.10.0" or "refs/tags/v0.10.0") is "v" + the version; with --notes, that the
//       release notes of the version (docs/release-notes/<version>.md) exist. Used by CI.
//   node tools/release/set-version.js --print               Prints the version (of VERSION).

'use strict';

const fs = require('fs');
const path = require('path');

const repoRoot = path.resolve(__dirname, '..', '..');
const versionFile = path.join(repoRoot, 'VERSION');

// The files containing the version. `pattern` must match exactly once; its first group is the
// version.
const targets = [
  {
    file: 'src/Server/VsChromiumVersion.cs',
    pattern: /public const string Product = "([^"]*)";/,
  },
  {
    // Visual Studio package assemblies (AssemblyInfo.cs) and InstalledProductRegistration.
    file: 'src/VsCore/VsChromiumVersion.cs',
    pattern: /public const string Product = "([^"]*)";/,
  },
  {
    file: 'src/VsChromium/source.extension.vsixmanifest',
    pattern: /<Identity Id="[^"]*" Version="([^"]*)"/,
  },
  {
    // The top level "version" property (2 spaces of indentation).
    file: 'src/vscode/package.json',
    pattern: /^ {2}"version": "([^"]*)"/m,
  },
  {
    file: 'src/Tools/Bench/Bench.csproj',
    pattern: /<Version>([^<]*)<\/Version>/,
  },
];

// major.minor.patch only: the VSIX manifest of Visual Studio and the assembly versions don't accept
// pre-release suffixes.
const versionPattern = /^\d+\.\d+\.\d+$/;

function fail(message) {
  console.error(`set-version: ${message}`);
  process.exit(1);
}

function readVersion() {
  if (!fs.existsSync(versionFile)) {
    fail(`${versionFile} not found.`);
  }
  const version = fs.readFileSync(versionFile, 'utf8').replace(/^\uFEFF/, '').trim();
  if (!versionPattern.test(version)) {
    fail(`invalid version in VERSION: "${version}" (expected major.minor.patch).`);
  }
  return version;
}

function findVersion(target, text) {
  const flags = target.pattern.flags.includes('g') ? target.pattern.flags : target.pattern.flags + 'g';
  const matches = [...text.matchAll(new RegExp(target.pattern.source, flags))];
  if (matches.length !== 1) {
    fail(`${target.file}: expected one match of ${target.pattern}, found ${matches.length}.`);
  }
  return matches[0];
}

function check(options) {
  const version = readVersion();
  const errors = [];
  for (const target of targets) {
    const text = fs.readFileSync(path.join(repoRoot, target.file), 'utf8');
    const found = findVersion(target, text)[1];
    if (found !== version) {
      errors.push(`${target.file}: "${found}", expected "${version}"`);
    }
  }
  if (options.tag !== undefined) {
    const tag = options.tag.replace(/^refs\/tags\//, '');
    if (tag !== 'v' + version) {
      errors.push(`tag "${tag}" doesn't match the version of VERSION: expected "v${version}"`);
    }
  }
  if (options.notes) {
    const notes = path.join(repoRoot, 'docs', 'release-notes', `${version}.md`);
    if (!fs.existsSync(notes) || fs.readFileSync(notes, 'utf8').trim().length === 0) {
      errors.push(`release notes missing (or empty): docs/release-notes/${version}.md`);
    }
  }
  if (errors.length > 0) {
    fail(`version check failed:\n  ${errors.join('\n  ')}\n` +
      `Run "node tools/release/set-version.js ${version}" to update the files (see docs/release.md).`);
  }
  console.log(`set-version: version ${version} OK (${targets.length} files${options.tag !== undefined ? ', tag' : ''}${options.notes ? ', release notes' : ''}).`);
}

function setVersion(version) {
  if (!versionPattern.test(version)) {
    fail(`invalid version "${version}" (expected major.minor.patch, e.g. 0.10.1).`);
  }
  for (const target of targets) {
    const file = path.join(repoRoot, target.file);
    const text = fs.readFileSync(file, 'utf8'); // A BOM, if any, is kept (first character).
    const match = findVersion(target, text);
    const start = match.index + match[0].indexOf(match[1]);
    const updated = text.slice(0, start) + version + text.slice(start + match[1].length);
    if (updated !== text) {
      fs.writeFileSync(file, updated, 'utf8');
      console.log(`  ${target.file}: ${match[1]} -> ${version}`);
    } else {
      console.log(`  ${target.file}: ${version} (unchanged)`);
    }
  }
  fs.writeFileSync(versionFile, version + '\r\n', 'utf8');
  console.log(`set-version: version ${version} (VERSION and ${targets.length} files).`);
}

function main(args) {
  if (args.length === 0) {
    fail('usage: set-version.js <major.minor.patch> | --check [--tag <tag>] [--notes] | --print');
  }
  if (args[0] === '--print') {
    console.log(readVersion());
    return;
  }
  if (args[0] === '--check') {
    const options = { notes: false };
    for (let i = 1; i < args.length; i++) {
      if (args[i] === '--notes') {
        options.notes = true;
      } else if (args[i] === '--tag' && i + 1 < args.length) {
        options.tag = args[++i];
      } else {
        fail(`unknown argument "${args[i]}".`);
      }
    }
    check(options);
    return;
  }
  setVersion(args[0]);
}

main(process.argv.slice(2));
