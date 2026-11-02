// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Tests.EndToEnd;

/// <summary>
/// Marks assertions that fail because of a known bug of the current server, to be fixed by the port
/// (see "Behavior changes" in docs/portable-server/PORTING_PRINCIPLES.md).
///
/// While the bug exists, the failing assertion makes the test <b>inconclusive</b> (reported as
/// skipped, with the reason). Once the bug is fixed, the assertion passes and the test <b>fails</b>,
/// as a reminder to remove the <see cref="KnownBug"/> marker so the test becomes a regular regression
/// test.
/// </summary>
public static class KnownBug {
  public static void Expect(int behaviorChange, string description, Action assertions) {
    try {
      assertions();
    } catch (AssertFailedException e) {
      Assert.Inconclusive($"Known bug (behavior change #{behaviorChange}: {description}), to be fixed by the port. {e.Message}");
      return;
    }
    Assert.Fail($"Behavior change #{behaviorChange} ({description}) now works: remove the KnownBug marker from this test.");
  }

  public static async Task ExpectAsync(int behaviorChange, string description, Func<Task> assertions) {
    try {
      await assertions();
    } catch (AssertFailedException e) {
      Assert.Inconclusive($"Known bug (behavior change #{behaviorChange}: {description}), to be fixed by the port. {e.Message}");
      return;
    }
    Assert.Fail($"Behavior change #{behaviorChange} ({description}) now works: remove the KnownBug marker from this test.");
  }
}
