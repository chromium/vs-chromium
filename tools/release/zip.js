// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Minimal zip writer and reader (no dependency), for the release packages (see release.js):
// - the same archives on every OS, with Unix permissions (executables are 0755, also in archives
//   created on Windows), UTF-8 names, deflate;
// - the verification of the packages (VSIX files are zip files).
// No zip64: entries and archives must be smaller than 4 GB.

'use strict';

const fs = require('fs');
const zlib = require('zlib');

function dosDateTime(date) {
  const year = Math.max(1980, date.getFullYear());
  return {
    time: (date.getHours() << 11) | (date.getMinutes() << 5) | Math.floor(date.getSeconds() / 2),
    date: ((year - 1980) << 9) | ((date.getMonth() + 1) << 5) | date.getDate(),
  };
}

/**
 * Writes a zip file. `entries`: [{ name: 'dir/file' (forward slashes), file: <path>, mode: 0o755 }].
 */
function writeZip(zipPath, entries) {
  const fd = fs.openSync(zipPath, 'w');
  const central = [];
  let offset = 0;
  const write = buffer => {
    fs.writeSync(fd, buffer);
    offset += buffer.length;
  };
  try {
    for (const entry of entries) {
      const data = fs.readFileSync(entry.file);
      const stat = fs.statSync(entry.file);
      const deflated = zlib.deflateRawSync(data, { level: 9 });
      const stored = deflated.length >= data.length;
      const content = stored ? data : deflated;
      const crc = zlib.crc32(data) >>> 0;
      const name = Buffer.from(entry.name, 'utf8');
      const { time, date } = dosDateTime(stat.mtime);
      if (data.length >= 0xFFFFFFFF || offset >= 0xFFFFFFFF) {
        throw new Error(`zip: ${entry.name}: too large (no zip64 support).`);
      }
      const header = Buffer.alloc(30);
      header.writeUInt32LE(0x04034b50, 0);
      header.writeUInt16LE(20, 4); // Version needed.
      header.writeUInt16LE(0x0800, 6); // UTF-8 names.
      header.writeUInt16LE(stored ? 0 : 8, 8);
      header.writeUInt16LE(time, 10);
      header.writeUInt16LE(date, 12);
      header.writeUInt32LE(crc, 14);
      header.writeUInt32LE(content.length, 18);
      header.writeUInt32LE(data.length, 22);
      header.writeUInt16LE(name.length, 26);
      header.writeUInt16LE(0, 28);
      const localOffset = offset;
      write(header);
      write(name);
      write(content);

      const record = Buffer.alloc(46);
      record.writeUInt32LE(0x02014b50, 0);
      record.writeUInt16LE((3 << 8) | 20, 4); // Made by: Unix (permissions in the external attributes).
      record.writeUInt16LE(20, 6);
      record.writeUInt16LE(0x0800, 8);
      record.writeUInt16LE(stored ? 0 : 8, 10);
      record.writeUInt16LE(time, 12);
      record.writeUInt16LE(date, 14);
      record.writeUInt32LE(crc, 16);
      record.writeUInt32LE(content.length, 20);
      record.writeUInt32LE(data.length, 24);
      record.writeUInt16LE(name.length, 28);
      record.writeUInt32LE((((0o100000 | (entry.mode || 0o644)) << 16) >>> 0), 38); // Regular file + permissions.
      record.writeUInt32LE(localOffset, 42);
      central.push(record, name);
    }
    const centralOffset = offset;
    for (const buffer of central) {
      write(buffer);
    }
    const end = Buffer.alloc(22);
    end.writeUInt32LE(0x06054b50, 0);
    end.writeUInt16LE(entries.length, 8);
    end.writeUInt16LE(entries.length, 10);
    end.writeUInt32LE(offset - centralOffset, 12);
    end.writeUInt32LE(centralOffset, 16);
    write(end);
  } finally {
    fs.closeSync(fd);
  }
}

/** The entries of a zip file: [{ name, size, compressedSize, method, mode, offset }]. */
function listZip(zipPath) {
  const buffer = fs.readFileSync(zipPath);
  let end = -1;
  for (let i = buffer.length - 22; i >= Math.max(0, buffer.length - 22 - 0xFFFF); i--) {
    if (buffer.readUInt32LE(i) === 0x06054b50) {
      end = i;
      break;
    }
  }
  if (end < 0) {
    throw new Error(`${zipPath}: not a zip file.`);
  }
  const count = buffer.readUInt16LE(end + 10);
  let position = buffer.readUInt32LE(end + 16);
  const entries = [];
  for (let i = 0; i < count; i++) {
    if (buffer.readUInt32LE(position) !== 0x02014b50) {
      throw new Error(`${zipPath}: invalid central directory.`);
    }
    const nameLength = buffer.readUInt16LE(position + 28);
    const extraLength = buffer.readUInt16LE(position + 30);
    const commentLength = buffer.readUInt16LE(position + 32);
    entries.push({
      name: buffer.toString('utf8', position + 46, position + 46 + nameLength),
      method: buffer.readUInt16LE(position + 10),
      compressedSize: buffer.readUInt32LE(position + 20),
      size: buffer.readUInt32LE(position + 24),
      madeBy: buffer.readUInt16LE(position + 4) >> 8,
      mode: (buffer.readUInt32LE(position + 38) >>> 16) & 0o777,
      offset: buffer.readUInt32LE(position + 42),
    });
    position += 46 + nameLength + extraLength + commentLength;
  }
  return entries;
}

/** The contents of an entry of a zip file (a Buffer), or null if there's no such entry. */
function readZipEntry(zipPath, name) {
  const entry = listZip(zipPath).find(e => e.name === name);
  if (!entry) {
    return null;
  }
  const fd = fs.openSync(zipPath, 'r');
  try {
    const header = Buffer.alloc(30);
    fs.readSync(fd, header, 0, 30, entry.offset);
    const dataOffset = entry.offset + 30 + header.readUInt16LE(26) + header.readUInt16LE(28);
    const content = Buffer.alloc(entry.compressedSize);
    fs.readSync(fd, content, 0, entry.compressedSize, dataOffset);
    if (entry.method === 0) {
      return content;
    }
    if (entry.method === 8) {
      return zlib.inflateRawSync(content);
    }
    throw new Error(`${zipPath}: ${name}: unsupported compression method ${entry.method}.`);
  } finally {
    fs.closeSync(fd);
  }
}

/**
 * The OS and architecture of an executable (PE, ELF or Mach-O header):
 * { os: 'win' | 'linux' | 'osx', arch: 'x64' | 'arm64' | 'other' }, or null if unknown.
 */
function executableKind(buffer) {
  if (buffer.length >= 64 && buffer.toString('latin1', 0, 2) === 'MZ') {
    const pe = buffer.readUInt32LE(0x3C);
    if (pe + 6 <= buffer.length && buffer.readUInt32LE(pe) === 0x00004550) {
      const machine = buffer.readUInt16LE(pe + 4);
      return { os: 'win', arch: machine === 0x8664 ? 'x64' : machine === 0xAA64 ? 'arm64' : 'other' };
    }
  }
  if (buffer.length >= 20 && buffer.readUInt32BE(0) === 0x7F454C46) {
    const machine = buffer.readUInt16LE(18);
    return { os: 'linux', arch: machine === 0x3E ? 'x64' : machine === 0xB7 ? 'arm64' : 'other' };
  }
  if (buffer.length >= 8 && buffer.readUInt32LE(0) === 0xFEEDFACF) {
    const cpu = buffer.readUInt32LE(4);
    return { os: 'osx', arch: cpu === 0x01000007 ? 'x64' : cpu === 0x0100000C ? 'arm64' : 'other' };
  }
  return null;
}

module.exports = { writeZip, listZip, readZipEntry, executableKind };
