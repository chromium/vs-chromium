// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

/** A file path search, possibly with a position (e.g. pasted from build output). */
export interface FilePathQuery {
  /** The path pattern to search for. */
  pattern: string;
  /** 0-based line number, if the input contained a position. */
  line?: number;
  /** 0-based column number, if the input contained a column. */
  column?: number;
}

// "path(12)" or "path(12,5)" (MSVC), optionally followed by ": error ..." etc.
const msvcPosition = /^(?<path>.+?)\((?<line>\d+)(?:\s*,\s*(?<col>\d+))?\)(?:\s*:.*)?$/;
// "path:12" or "path:12:5" (clang, gcc, ...), optionally followed by ": error ..." etc. The path
// is matched lazily (so "a.cc:12:5" is not parsed as path "a.cc:12") and must not end with ":"
// so that "C:" (drive letter) is never parsed as a line number.
const clangPosition = /^(?<path>.*?[^:\s]):(?<line>\d+)(?::(?<col>\d+))?(?:\s*:.*)?$/;

/**
 * Parses a file path search string. Understands paths copied from build output, like
 * "base/foo.cc(19,18)" or "../../base/foo.cc:19:18: error: ...", in which case the position is
 * returned separately (and is used to open the file at that location).
 */
export function parseFilePathQuery(text: string): FilePathQuery {
  const trimmed = text.trim();
  const match = msvcPosition.exec(trimmed) ?? clangPosition.exec(trimmed);
  if (!match?.groups) {
    return { pattern: trimmed };
  }
  const line = Number(match.groups.line);
  const col = match.groups.col !== undefined ? Number(match.groups.col) : undefined;
  return {
    pattern: stripRelativePrefix(match.groups.path.trim()),
    line: Math.max(0, line - 1),
    column: col !== undefined ? Math.max(0, col - 1) : undefined,
  };
}

/**
 * Removes leading "./" and "../" components (e.g. "../../base/foo.cc" in Chromium build output,
 * relative to the output directory), which never match indexed paths.
 */
function stripRelativePrefix(path: string): string {
  return path.replace(/^(?:\.\.?[\\/])+/, '');
}
