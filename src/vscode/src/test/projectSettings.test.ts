// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { folderForProject, isSameOrInside } from '../ui/projectSettings';

describe('projectSettings', () => {
  const root = path.resolve('/work/chromium/src');

  it('compares paths', () => {
    assert.equal(isSameOrInside(root, root), true);
    assert.equal(isSameOrInside(path.join(root, 'base'), root), true);
    assert.equal(isSameOrInside(root + 'x', root), false, 'Not a prefix of a name');
    assert.equal(isSameOrInside(path.dirname(root), root), false);
    assert.equal(isSameOrInside(root.toUpperCase(), root, 'win32'), true, 'Case insensitive on Windows');
  });

  it('uses the deepest folder containing the project', () => {
    const work = path.resolve('/work');
    const chromium = path.resolve('/work/chromium');
    assert.equal(folderForProject(root, [work, chromium]), chromium);
    assert.equal(folderForProject(root, [root]), root);
  });

  it('uses a folder inside the project otherwise', () => {
    const base = path.join(root, 'base');
    const other = path.resolve('/other');
    assert.equal(folderForProject(root, [other, base]), base);
  });

  it('returns undefined without related folder', () => {
    assert.equal(folderForProject(root, [path.resolve('/other')]), undefined);
    assert.equal(folderForProject(root, []), undefined);
  });
});
