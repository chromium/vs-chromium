// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { buildViewModel, DefaultLabels } from '../ui/dashboard/dashboardModel';
import { applyThemeVariables, ThemeStyle } from '../ui/dashboard/theme';

class FakeStyle implements ThemeStyle {
  readonly values = new Map<string, string>();
  setProperty(name: string, value: string): void {
    this.values.set(name, value);
  }
  removeProperty(name: string): string {
    const value = this.values.get(name) ?? '';
    this.values.delete(name);
    return value;
  }
}

describe('applyThemeVariables', () => {
  it('sets the variables of the page', () => {
    const style = new FakeStyle();
    const applied = applyThemeVariables(style, { '--vscode-foreground': '#cccccc', '--vscode-font-family': '"Segoe UI", sans-serif' });
    assert.deepEqual([...style.values], [['--vscode-foreground', '#cccccc'], ['--vscode-font-family', '"Segoe UI", sans-serif']]);
    assert.deepEqual([...applied], ['--vscode-foreground', '--vscode-font-family']);
  });

  it('ignores other properties and values that could escape the declaration', () => {
    const style = new FakeStyle();
    applyThemeVariables(style, {
      'color': 'red',
      '--other': 'red',
      '--vscode-x': 'red; background: url(x)',
      '--vscode-y': '} body { color: red',
      '--vscode-ok': 'rgba(0, 0, 0, 0.5)',
    });
    assert.deepEqual([...style.values.keys()], ['--vscode-ok']);
  });

  it('removes the variables of the previous theme that are not in the new one', () => {
    const style = new FakeStyle();
    const first = applyThemeVariables(style, { '--vscode-contrastBorder': '#6fc3df', '--vscode-foreground': '#fff' });
    applyThemeVariables(style, { '--vscode-foreground': '#333' }, first);
    assert.deepEqual([...style.values], [['--vscode-foreground', '#333']]);
  });
});

describe('host labels', () => {
  it('uses the wording of the host, or the VS Code wording', () => {
    const host = { hostName: 'Test', serverRunning: true, trigramFiltersSetting: false };
    assert.equal(buildViewModel({ host }).labels.trigramFiltersDefault, DefaultLabels.trigramFiltersDefault);
    assert.equal(buildViewModel({ host: { ...host, labels: { trigramFiltersDefault: 'Tools > Options' } } }).labels.trigramFiltersDefault,
      'Tools > Options');
  });
});
