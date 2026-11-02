// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { SearchCodeResponse } from '../server/messages';
import { describeResults, flattenSearchResults, toDisplayLine } from '../ui/codeSearchModel';

const response: SearchCodeResponse = {
  $type: 'SearchCodeResponse',
  hitCount: 4,
  searchedFileCount: 10,
  totalFileCount: 12,
  searchResults: {
    name: '',
    entries: [
      {
        $type: 'DirectoryEntry',
        name: 'C:\\src\\chromium\\src',
        entries: [
          { $type: 'FileEntry', name: 'base\\foo.cc', data: { $type: 'FilePositionsData', positions: [{ position: 10, length: 3 }] } },
          {
            $type: 'DirectoryEntry',
            name: 'content',
            entries: [{ $type: 'FileEntry', name: 'bar.h', data: { $type: 'FilePositionsData', positions: [{ position: 1, length: 3 }, { position: 50, length: 3 }] } }],
          },
        ],
      },
      {
        $type: 'DirectoryEntry',
        name: 'D:\\other',
        entries: [{ $type: 'FileEntry', name: 'readme.txt' }],
      },
    ],
  },
};

describe('flattenSearchResults', () => {
  it('flattens projects, nested directories and files', () => {
    const files = flattenSearchResults(response);
    assert.deepEqual(files.map(f => [f.index, f.fullPath, f.relativePath, f.projectRoot, f.positions.length]), [
      [0, path.join('C:\\src\\chromium\\src', 'base\\foo.cc'), 'base\\foo.cc', 'C:\\src\\chromium\\src', 1],
      [1, path.join('C:\\src\\chromium\\src', 'content', 'bar.h'), path.join('content', 'bar.h'), 'C:\\src\\chromium\\src', 2],
      [2, path.join('D:\\other', 'readme.txt'), 'readme.txt', 'D:\\other', 0],
    ]);
  });

  it('handles empty results', () => {
    assert.deepEqual(flattenSearchResults({ ...response, searchResults: { name: '' } }), []);
  });
});

describe('toDisplayLine', () => {
  it('splits the line around the match and converts to 1-based line/column', () => {
    const line = toDisplayLine(
      { text: '    int foo = bar;  ', offset: 100, length: 20, lineNumber: 4, columnNumber: 8 },
      { position: 108, length: 3 });
    assert.deepEqual(line, { lineNumber: 5, columnNumber: 9, before: 'int ', match: 'foo', after: ' = bar;' });
  });

  it('clamps matches extending past a truncated extract', () => {
    const line = toDisplayLine(
      { text: 'abcdef', offset: 0, length: 6, lineNumber: 0, columnNumber: 4 },
      { position: 4, length: 10 });
    assert.deepEqual([line.before, line.match, line.after], ['abcd', 'ef', '']);
  });
});

describe('describeResults', () => {
  it('summarizes hits, files and searched files', () => {
    assert.equal(describeResults(response, 2, 10000, 15), '4 results in 2 files, searched 10 of 12 files in 15 ms');
  });

  it('mentions when results are truncated', () => {
    assert.match(describeResults({ ...response, hitCount: 100 }, 2, 100, 1), /limited to 100/);
  });
});
