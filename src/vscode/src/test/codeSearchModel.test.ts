// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { SearchCodeResult, SearchPathsResult } from '../protocol/protocol.g';
import { describeResults, flattenFilePaths, flattenSearchResults, toDisplayLine } from '../ui/codeSearchModel';

const result: SearchCodeResult = {
  hitCount: 4,
  searchedFileCount: 10,
  totalFileCount: 12,
  projects: [
    {
      rootPath: 'C:\\src\\chromium\\src',
      files: [
        { relativePath: 'base\\foo.cc', matches: [{ position: 10, length: 3 }] },
        { relativePath: 'content\\bar.h', matches: [{ position: 1, length: 3 }, { position: 50, length: 3 }] },
      ],
    },
    {
      rootPath: 'D:\\other',
      files: [{ relativePath: 'readme.txt', matches: [] }],
    },
  ],
};

describe('flattenSearchResults', () => {
  it('maps the files of each project (flat result) to the list displayed by the view', () => {
    const files = flattenSearchResults(result);
    assert.deepEqual(files.map(f => [f.index, f.fullPath, f.relativePath, f.projectRoot, f.spans.length]), [
      [0, path.join('C:\\src\\chromium\\src', 'base\\foo.cc'), path.normalize('base\\foo.cc'), 'C:\\src\\chromium\\src', 1],
      [1, path.join('C:\\src\\chromium\\src', 'content\\bar.h'), path.normalize('content\\bar.h'), 'C:\\src\\chromium\\src', 2],
      [2, path.join('D:\\other', 'readme.txt'), 'readme.txt', 'D:\\other', 0],
    ]);
    assert.deepEqual(files[1].spans, [{ position: 1, length: 3 }, { position: 50, length: 3 }]);
  });

  it('normalizes the separators of the relative paths', () => {
    const files = flattenSearchResults({
      ...result,
      projects: [{ rootPath: path.resolve('root'), files: [{ relativePath: 'a/b/c.h', matches: [] }] }],
    });
    assert.equal(files[0].relativePath, path.join('a', 'b', 'c.h'));
    assert.equal(files[0].fullPath, path.join(path.resolve('root'), 'a', 'b', 'c.h'));
  });

  it('handles empty results', () => {
    assert.deepEqual(flattenSearchResults({ ...result, projects: [] }), []);
  });
});

describe('flattenFilePaths', () => {
  it('maps the relative paths of each project to files', () => {
    const paths: SearchPathsResult = {
      hitCount: 3,
      totalCount: 100,
      projects: [
        { rootPath: 'C:\\a', relativePaths: ['x.cc', 'dir\\y.h'] },
        { rootPath: 'C:\\b', relativePaths: ['z.txt'] },
      ],
    };
    assert.deepEqual(flattenFilePaths(paths).map(f => [f.index, f.fullPath, f.projectRoot, f.spans.length]), [
      [0, path.join('C:\\a', 'x.cc'), 'C:\\a', 0],
      [1, path.join('C:\\a', 'dir\\y.h'), 'C:\\a', 0],
      [2, path.join('C:\\b', 'z.txt'), 'C:\\b', 0],
    ]);
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
    assert.equal(describeResults(result, 2, 10000, 15), '4 results in 2 files, searched 10 of 12 files in 15 ms');
  });

  it('mentions when results are truncated', () => {
    assert.match(describeResults({ ...result, hitCount: 100 }, 2, 100, 1), /limited to 100/);
  });
});
