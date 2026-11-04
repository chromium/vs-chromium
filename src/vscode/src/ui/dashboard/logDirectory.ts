// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

import * as path from 'path';

/**
 * The directory of the log files of the server: "VsChromium" in the .NET local application data
 * folder (see src/Server/Logging/NLogConfig.cs): %LOCALAPPDATA% on Windows,
 * ~/Library/Application Support on macOS, $XDG_DATA_HOME or ~/.local/share on Linux.
 */
export function serverLogDirectory(
  platform: string,
  env: Record<string, string | undefined>,
  homeDirectory: string,
): string {
  if (platform === 'win32') {
    const localAppData = env.LOCALAPPDATA || path.win32.join(homeDirectory, 'AppData', 'Local');
    return path.win32.join(localAppData, 'VsChromium');
  }
  if (platform === 'darwin') {
    return path.posix.join(homeDirectory, 'Library', 'Application Support', 'VsChromium');
  }
  const dataHome = env.XDG_DATA_HOME || path.posix.join(homeDirectory, '.local', 'share');
  return path.posix.join(dataHome, 'VsChromium');
}
