// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// The theme of the page sent by hosts other than VS Code (e.g. Visual Studio): the values of the
// CSS variables used by the page ("--vscode-*"), applied on the root element through the CSSOM
// (allowed by a strict Content Security Policy, unlike inline style sheets). Free of DOM types
// (unit tested with a fake style).

/** The part of `CSSStyleDeclaration` used to apply the theme. */
export interface ThemeStyle {
  setProperty(name: string, value: string): void;
  removeProperty(name: string): string;
}

const VariableName = /^--vscode-[A-Za-z0-9-]+$/;

/**
 * Applies the theme `variables` to `style`: sets the valid variables ("--vscode-*", values without
 * ";", "{", "}"), removes the variables of the previous theme that are not in the new one. Returns
 * the names of the variables set (the "previous" of the next call).
 */
export function applyThemeVariables(
  style: ThemeStyle,
  variables: Record<string, string>,
  previous: ReadonlySet<string> = new Set(),
): Set<string> {
  const applied = new Set<string>();
  for (const [name, value] of Object.entries(variables ?? {})) {
    if (!VariableName.test(name) || typeof value !== 'string' || /[;{}]/.test(value)) {
      continue;
    }
    style.setProperty(name, value);
    applied.add(name);
  }
  for (const name of previous) {
    if (!applied.has(name)) {
      style.removeProperty(name);
    }
  }
  return applied;
}
