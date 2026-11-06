// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as path from 'path';

/**
 * Per project settings ("vschromium.trigramFilters", "vschromium.cacheEnabled",
 * "vschromium.watchExternalLinkTargets": resource scoped, i.e. per workspace folder): the workspace folder whose settings apply to a project.
 */

/** True if `child` is `parent` or a path inside it (case-insensitive on Windows). */
export function isSameOrInside(child: string, parent: string, platform: NodeJS.Platform = process.platform): boolean {
  const normalize = (p: string) => {
    const resolved = path.resolve(p).replace(/[\\/]+$/, '');
    return platform === 'win32' ? resolved.toLowerCase() : resolved;
  };
  const c = normalize(child);
  const p = normalize(parent);
  return c === p || c.startsWith(p + path.sep);
}

/**
 * The workspace folder whose settings apply to the project whose root is `projectRoot`: the folder
 * containing the project root (the deepest one), otherwise the first folder inside the project
 * (e.g. a sub-directory of a large checkout opened as a folder), otherwise undefined (the workspace
 * or user settings apply).
 */
export function folderForProject(projectRoot: string, folders: readonly string[],
                                 platform: NodeJS.Platform = process.platform): string | undefined {
  const containing = folders
    .filter(folder => isSameOrInside(projectRoot, folder, platform))
    .sort((a, b) => b.length - a.length);
  if (containing.length > 0) {
    return containing[0];
  }
  return folders.find(folder => isSameOrInside(folder, projectRoot, platform));
}
