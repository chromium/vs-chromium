// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as path from 'path';
import { FileExtract, SearchCodeResult, SearchPathsResult, TextSpan } from '../protocol/protocol.g';

/** A file of the search results (projects[].files[] of "search/code", flattened). */
export interface SearchResultFile {
  /** Index in the result list (stable identifier used by the webview). */
  index: number;
  fullPath: string;
  /** Path relative to the project root, used for display. */
  relativePath: string;
  projectRoot: string;
  /** The matches in the file (empty for file path searches). */
  spans: TextSpan[];
}

/**
 * Builds the list of files displayed by the Code Search view from the result of "search/code":
 * the files of each project, in the order of the result, with their match spans.
 */
export function flattenSearchResults(result: SearchCodeResult): SearchResultFile[] {
  const files: SearchResultFile[] = [];
  for (const project of result.projects) {
    for (const file of project.files) {
      files.push(createFile(files.length, project.rootPath, file.relativePath, file.matches));
    }
  }
  return files;
}

/** Builds the list of files from the result of "search/paths" (no match spans). */
export function flattenFilePaths(result: SearchPathsResult): SearchResultFile[] {
  const files: SearchResultFile[] = [];
  for (const project of result.projects) {
    for (const relativePath of project.relativePaths) {
      files.push(createFile(files.length, project.rootPath, relativePath, []));
    }
  }
  return files;
}

function createFile(index: number, projectRoot: string, relativePath: string, spans: TextSpan[]): SearchResultFile {
  // path.normalize: relative paths use the separator of the server platform.
  const normalizedRelativePath = path.normalize(relativePath);
  return {
    index,
    fullPath: path.join(projectRoot, normalizedRelativePath),
    relativePath: normalizedRelativePath,
    projectRoot,
    spans,
  };
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
 * Builds the display line for a match from its file extract ("files/extracts"). The extract text
 * is the line containing the match; `span` is the match location in the file.
 */
export function toDisplayLine(extract: FileExtract, span: TextSpan): DisplayLine {
  const text = extract.text;
  const start = clamp(span.position - extract.offset, 0, text.length);
  const end = clamp(start + span.length, start, text.length);
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
export function describeResults(result: SearchCodeResult, fileCount: number, maxResults: number, elapsedMs: number): string {
  const hits = result.hitCount;
  const truncated = hits >= maxResults ? ` (limited to ${maxResults.toLocaleString()})` : '';
  const files = `${fileCount.toLocaleString()} file${fileCount === 1 ? '' : 's'}`;
  const searched = `searched ${result.searchedFileCount.toLocaleString()} of ${result.totalFileCount.toLocaleString()} files`;
  return `${hits.toLocaleString()} result${hits === 1 ? '' : 's'}${truncated} in ${files}, ${searched} in ${elapsedMs.toLocaleString()} ms`;
}
