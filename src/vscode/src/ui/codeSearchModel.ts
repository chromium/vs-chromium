// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as path from 'path';
import { DirectoryEntry, FileExtract, FilePositionSpan, SearchCodeResponse } from '../server/messages';

/** A file containing matches, flattened from the SearchCodeResponse tree. */
export interface SearchResultFile {
  /** Index in the result list (stable identifier used by the webview). */
  index: number;
  fullPath: string;
  /** Path relative to the project root, used for display. */
  relativePath: string;
  projectRoot: string;
  positions: FilePositionSpan[];
}

/**
 * Flattens the search results tree (root -> projects -> files, possibly with nested directories)
 * into a list of files with their match positions.
 */
export function flattenSearchResults(response: SearchCodeResponse): SearchResultFile[] {
  return flattenDirectoryEntry(response.searchResults);
}

/**
 * Flattens a search result tree, as returned by SearchCode and SearchFilePaths requests: the
 * root entry contains one directory entry per project (name = project root path), containing
 * files and directories (names relative to their parent).
 */
export function flattenDirectoryEntry(root: DirectoryEntry): SearchResultFile[] {
  const files: SearchResultFile[] = [];
  for (const project of root.entries ?? []) {
    if (!isDirectory(project)) {
      continue;
    }
    const visit = (dir: DirectoryEntry, relativeDir: string) => {
      for (const entry of dir.entries ?? []) {
        const relativePath = relativeDir ? path.join(relativeDir, entry.name) : entry.name;
        if (isDirectory(entry)) {
          visit(entry, relativePath);
        } else {
          files.push({
            index: files.length,
            fullPath: path.join(project.name, relativePath),
            relativePath,
            projectRoot: project.name,
            positions: entry.data?.positions ?? [],
          });
        }
      }
    };
    visit(project, '');
  }
  return files;
}

function isDirectory(entry: { $type?: string }): entry is DirectoryEntry {
  return entry.$type === undefined || entry.$type === 'DirectoryEntry';
}

/** A line of text to display in the results, with the match highlighted. */
export interface DisplayLine {
  /** 1-based line number, for display. */
  lineNumber: number;
  /** 1-based column number, for display. */
  columnNumber: number;
  /** Text before the match (leading whitespace trimmed). */
  before: string;
  match: string;
  after: string;
}

/**
 * Builds the display line for a match from its file extract. The extract text is the line
 * containing the match; `position` is the match location in the file.
 */
export function toDisplayLine(extract: FileExtract, position: FilePositionSpan): DisplayLine {
  const text = extract.text;
  const start = clamp(position.position - extract.offset, 0, text.length);
  const end = clamp(start + position.length, start, text.length);
  return {
    lineNumber: extract.lineNumber + 1,
    columnNumber: extract.columnNumber + 1,
    before: text.substring(0, start).trimStart(),
    match: text.substring(start, end),
    after: text.substring(end).trimEnd(),
  };
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

/** Summary line displayed above the results. */
export function describeResults(response: SearchCodeResponse, fileCount: number, maxResults: number, elapsedMs: number): string {
  const hits = response.hitCount;
  const truncated = hits >= maxResults ? ` (limited to ${maxResults.toLocaleString()})` : '';
  const files = `${fileCount.toLocaleString()} file${fileCount === 1 ? '' : 's'}`;
  const searched = `searched ${response.searchedFileCount.toLocaleString()} of ${response.totalFileCount.toLocaleString()} files`;
  return `${hits.toLocaleString()} result${hits === 1 ? '' : 's'}${truncated} in ${files}, ${searched} in ${elapsedMs.toLocaleString()} ms`;
}
