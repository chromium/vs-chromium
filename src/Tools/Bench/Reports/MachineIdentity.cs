// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Bench.Reports;

/// <summary>
/// The anonymous id of the machine: a random GUID generated once and stored in the data directory of
/// the bench (never derived from the hardware or the user), so that the shareable reports of the same
/// machine can be grouped without identifying it.
/// </summary>
public static class MachineIdentity {
  public const string FileName = "machine-id";

  /// <summary>The id stored in <paramref name="dataDirectory"/>, created if missing or invalid.</summary>
  public static Guid GetOrCreate(string dataDirectory) {
    var path = Path.Combine(dataDirectory, FileName);
    try {
      if (File.Exists(path) && Guid.TryParse(File.ReadAllText(path).Trim(), out var existing) && existing != Guid.Empty) {
        return existing;
      }
    } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      // Unreadable: a new id.
    }
    var id = Guid.NewGuid();
    Directory.CreateDirectory(dataDirectory);
    File.WriteAllText(path, id.ToString("D") + Environment.NewLine);
    return id;
  }

  /// <summary>The short form shown in the reports: the first 8 hexadecimal digits.</summary>
  public static string Short(Guid id) => id.ToString("N")[..8];

  public static string Short(string id) => Guid.TryParse(id, out var guid) ? Short(guid) : id.Length > 8 ? id[..8] : id;
}
