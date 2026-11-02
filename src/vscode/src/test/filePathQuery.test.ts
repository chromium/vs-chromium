// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { parseFilePathQuery } from '../ui/filePathQuery';

describe('parseFilePathQuery', () => {
  const cases: [string, ReturnType<typeof parseFilePathQuery>][] = [
    ['foo.cc', { pattern: 'foo.cc' }],
    ['  base\\foo.cc  ', { pattern: 'base\\foo.cc' }],
    ['*.mojom', { pattern: '*.mojom' }],
    // MSVC style
    ['base\\foo.cc(19)', { pattern: 'base\\foo.cc', line: 18, column: undefined }],
    ['base\\foo.cc(19,18)', { pattern: 'base\\foo.cc', line: 18, column: 17 }],
    ['base\\foo.cc(19, 18): error C2065: undeclared identifier', { pattern: 'base\\foo.cc', line: 18, column: 17 }],
    ['C:\\src\\chromium\\src\\base\\foo.cc(7)', { pattern: 'C:\\src\\chromium\\src\\base\\foo.cc', line: 6, column: undefined }],
    // clang / gcc style
    ['base/foo.cc:19', { pattern: 'base/foo.cc', line: 18, column: undefined }],
    ['base/foo.cc:19:18', { pattern: 'base/foo.cc', line: 18, column: 17 }],
    ['../../base/foo.cc:19:18: error: use of undeclared identifier', { pattern: 'base/foo.cc', line: 18, column: 17 }],
    ['C:\\src\\foo.cc:3:1', { pattern: 'C:\\src\\foo.cc', line: 2, column: 0 }],
    // Drive letters are not positions.
    ['C:\\src\\foo.cc', { pattern: 'C:\\src\\foo.cc' }],
    ['C:', { pattern: 'C:' }],
    // Line 0 is clamped.
    ['foo.cc:0', { pattern: 'foo.cc', line: 0, column: undefined }],
  ];
  for (const [input, expected] of cases) {
    it(`parses ${JSON.stringify(input)}`, () => {
      assert.deepEqual(parseFilePathQuery(input), expected);
    });
  }
});
