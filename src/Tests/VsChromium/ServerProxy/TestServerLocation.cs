// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.ServerProxy;

namespace VsChromium.Tests.VsChromium.ServerProxy {
  [TestClass]
  public class TestServerLocation {
    [TestMethod]
    public void NativeArchitectureWinsOverTheProcessArchitecture() {
      // x64 Visual Studio emulated on ARM64 Windows: IsWow64Process2 reports
      // the native machine (ARM64), while the OS architecture seen by the
      // emulated process may be X64.
      Assert.AreEqual("win-arm64", ServerLocation.GetRuntimeIdentifier(ServerLocation.ImageFileMachineArm64, Architecture.X64));
      Assert.AreEqual("win-x64", ServerLocation.GetRuntimeIdentifier(ServerLocation.ImageFileMachineAmd64, Architecture.Arm64));
    }

    [TestMethod]
    public void OsArchitectureIsTheFallback() {
      Assert.AreEqual("win-arm64", ServerLocation.GetRuntimeIdentifier(ServerLocation.ImageFileMachineUnknown, Architecture.Arm64));
      Assert.AreEqual("win-x64", ServerLocation.GetRuntimeIdentifier(ServerLocation.ImageFileMachineUnknown, Architecture.X64));
      Assert.IsNull(ServerLocation.GetRuntimeIdentifier(ServerLocation.ImageFileMachineUnknown, Architecture.X86));
      Assert.IsNull(ServerLocation.GetRuntimeIdentifier(ServerLocation.ImageFileMachineI386, Architecture.X64));
    }

    [TestMethod]
    public void CandidatePaths() {
      CollectionAssert.AreEqual(
        new[] { @"Server\win-arm64\VsChromium.Server.exe", @"Server\win-x64\VsChromium.Server.exe", @"Server\VsChromium.Server.exe" },
        ServerLocation.GetCandidatePaths("win-arm64").ToArray());
      CollectionAssert.AreEqual(
        new[] { @"Server\win-x64\VsChromium.Server.exe", @"Server\VsChromium.Server.exe" },
        ServerLocation.GetCandidatePaths("win-x64").ToArray());
      CollectionAssert.AreEqual(
        new[] { @"Server\VsChromium.Server.exe" },
        ServerLocation.GetCandidatePaths(null).ToArray());
    }

    [TestMethod]
    public void FindServerPathPrefersTheNativeServer() {
      var package = @"C:\Extensions\VsChromium";
      var vsix = new HashSet<string> {
        Path.Combine(package, @"Server\win-x64\VsChromium.Server.exe"),
        Path.Combine(package, @"Server\win-arm64\VsChromium.Server.exe"),
      };
      Assert.AreEqual(Path.Combine(package, @"Server\win-arm64\VsChromium.Server.exe"),
        ServerLocation.FindServerPath(package, "win-arm64", vsix.Contains));
      Assert.AreEqual(Path.Combine(package, @"Server\win-x64\VsChromium.Server.exe"),
        ServerLocation.FindServerPath(package, "win-x64", vsix.Contains));

      // Developer build (framework-dependent server in Server\).
      var debug = new HashSet<string> { Path.Combine(package, @"Server\VsChromium.Server.exe") };
      Assert.AreEqual(Path.Combine(package, @"Server\VsChromium.Server.exe"),
        ServerLocation.FindServerPath(package, "win-arm64", debug.Contains));

      // An x64 server only: emulated on ARM64.
      var x64Only = new HashSet<string> { Path.Combine(package, @"Server\win-x64\VsChromium.Server.exe") };
      Assert.AreEqual(Path.Combine(package, @"Server\win-x64\VsChromium.Server.exe"),
        ServerLocation.FindServerPath(package, "win-arm64", x64Only.Contains));
      Assert.IsNull(ServerLocation.FindServerPath(package, "win-x64", new HashSet<string>().Contains));
    }

    [TestMethod]
    public void NativeRuntimeIdentifierOfThisMachine() {
      var expected = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
      // The test host may be emulated: the native machine is reported anyway.
      var native = ServerLocation.GetNativeMachine();
      if (native == ServerLocation.ImageFileMachineArm64) {
        expected = "win-arm64";
      }
      Assert.AreEqual(expected, ServerLocation.GetNativeRuntimeIdentifier());
    }
  }
}
