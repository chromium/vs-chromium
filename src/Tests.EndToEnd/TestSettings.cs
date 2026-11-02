// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Reflection;

namespace VsChromium.Tests.EndToEnd;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class RepositoryRootAttribute(string path) : Attribute {
  public string Path { get; } = path;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class BuildConfigurationAttribute(string configuration) : Attribute {
  public string Configuration { get; } = configuration;
}

/// <summary>
/// Settings of the end-to-end tests, from environment variables (with defaults suitable for running
/// from a developer's repository).
/// </summary>
public static class TestSettings {
  private static readonly Assembly ThisAssembly = typeof(TestSettings).Assembly;

  public static string RepositoryRoot =>
    ThisAssembly.GetCustomAttribute<RepositoryRootAttribute>()!.Path;

  /// <summary>
  /// Path of the server executable under test: VSCHROMIUM_SERVER_PATH, or the server built in
  /// <c>Binaries/&lt;Configuration&gt;/Server</c> of the repository.
  /// </summary>
  public static string ServerPath {
    get {
      var path = Environment.GetEnvironmentVariable("VSCHROMIUM_SERVER_PATH");
      if (!string.IsNullOrEmpty(path)) {
        return path;
      }
      var configuration = ThisAssembly.GetCustomAttribute<BuildConfigurationAttribute>()!.Configuration;
      var exeName = OperatingSystem.IsWindows() ? "VsChromium.Server.exe" : "VsChromium.Server";
      return System.IO.Path.Combine(RepositoryRoot, "Binaries", configuration, "Server", exeName);
    }
  }

  /// <summary>
  /// Directory where generated test code bases are kept between test runs: VSCHROMIUM_TEST_CORPORA,
  /// or <c>&lt;LocalApplicationData&gt;/VsChromium/TestCorpora</c>.
  /// </summary>
  public static string CorporaDirectory {
    get {
      var path = Environment.GetEnvironmentVariable("VSCHROMIUM_TEST_CORPORA");
      if (!string.IsNullOrEmpty(path)) {
        return path;
      }
      return System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "VsChromium", "TestCorpora");
    }
  }

  /// <summary>
  /// Size of the code base used by benchmarks: VSCHROMIUM_BENCHMARK_SIZE (small, medium, large),
  /// default small.
  /// </summary>
  public static string BenchmarkSize =>
    Environment.GetEnvironmentVariable("VSCHROMIUM_BENCHMARK_SIZE") is { Length: > 0 } size ? size : "small";

  /// <summary>
  /// Directory where benchmark results are written: VSCHROMIUM_BENCHMARK_RESULTS, or
  /// <c>&lt;repo&gt;/src/Tests.EndToEnd/bin/BenchmarkResults</c>.
  /// </summary>
  public static string BenchmarkResultsDirectory =>
    Environment.GetEnvironmentVariable("VSCHROMIUM_BENCHMARK_RESULTS") is { Length: > 0 } dir
      ? dir
      : System.IO.Path.Combine(RepositoryRoot, "src", "Tests.EndToEnd", "bin", "BenchmarkResults");
}
