// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;
using VsChromium.Tests.EndToEnd.Corpus;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// Regression tests for the behavior changes of the port (bugs of the current server), see
/// "Behavior changes" in docs/portable-server/PORTING_PRINCIPLES.md. Each test describes the correct
/// behavior; tests failing on the current server are wrapped in <see cref="KnownBug"/>.
/// </summary>
[TestClass]
public class BehaviorChangeTests {
  private static StdioServerClient? s_server;
  private static string s_root = "";

  private static StdioServerClient Server => s_server ?? throw new InvalidOperationException("Server not started");

  // --- Test files ------------------------------------------------------------------------------

  private const string OffsetsFile = "offsets.txt";
  private const string OffsetsText =
    "ASCII only line\n" +
    "Ünïcödé prefix: OFFSET_MARKER_LATIN here\n" +
    "日本語 prefix: OFFSET_MARKER_CJK here\n" +
    "emoji 🎉 prefix: OFFSET_MARKER_EMOJI here\n";

  private const string ChunkFile = "chunk_boundary.txt";
  private const string ChunkBoundaryMarker = "CHUNK_BOUNDARY_MARKER";
  private const string ChunkControlMarker = "CHUNK_CONTROL_MARKER";
  /// <summary>Chunk size of the current server (FileDatabaseBuilder.ChunkSize).</summary>
  private const int ChunkSize = 100 * 1024;

  private const string NonAsciiFile = "non_ascii_pattern.txt";
  private const string NonAsciiText =
    "line with 検索テスト_NONASCII_PATTERN in it\n" +
    "line with ÜBER_NONASCII_CASE in it\n" +
    "line with héllo_regex_marker in it\n" +
    "line with äöü_regex_class in it\n";

  private static void GenerateFiles(string root) {
    File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"),
      "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
    var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    File.WriteAllText(Path.Combine(root, OffsetsFile), OffsetsText, utf8);
    File.WriteAllText(Path.Combine(root, NonAsciiFile), NonAsciiText, utf8);

    // ASCII file of 250 KB, with a marker spanning the first chunk boundary and a control marker.
    var sb = new StringBuilder();
    sb.Append(ChunkControlMarker).Append('\n');
    while (sb.Length < ChunkSize - 10) {
      sb.Append("filler line of text for the chunk boundary test\n");
    }
    sb.Length = ChunkSize - 10;
    sb.Append(ChunkBoundaryMarker).Append('\n');
    while (sb.Length < 250 * 1024) {
      sb.Append("more filler text after the chunk boundary marker\n");
    }
    File.WriteAllText(Path.Combine(root, ChunkFile), sb.ToString(), utf8);
  }

  // --- Setup -----------------------------------------------------------------------------------

  [ClassInitialize]
  public static async Task ClassInitialize(TestContext context) {
    s_root = CorpusCache.GetOrCreateCustom("behavior-changes", version: 1, GenerateFiles);
    s_server = await StdioServerClient.StartAsync(TestSettings.ServerPath);
    await Server.WaitForEventAsync("SearchEngineFilesLoaded", TimeSpan.FromMinutes(2),
      () => Server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(s_root, "vs-chromium-project.txt"))));
  }

  [ClassCleanup]
  public static async Task ClassCleanup() {
    if (s_server != null) {
      await s_server.DisposeAsync();
    }
  }

  private static async Task<(long HitCount, List<SearchResultFile> Files)> SearchAsync(string text, bool matchCase = true,
                                                                                     bool regex = false, bool useRe2Engine = true) {
    var response = await Server.RequestAsync(ServerProtocol.SearchCode(text, matchCase: matchCase, regex: regex, useRe2Engine: useRe2Engine));
    return (response.Data["hitCount"]!.GetValue<long>(), ServerProtocol.FlattenResults(response.Data["searchResults"]?.AsObject()));
  }

  // --- #1: positions are UTF-16 offsets ------------------------------------------------------

  [TestMethod]
  [DataRow("OFFSET_MARKER_LATIN")]
  [DataRow("OFFSET_MARKER_CJK")]
  [DataRow("OFFSET_MARKER_EMOJI")]
  public async Task Positions_AreUtf16Offsets(string marker) {
    var (hits, files) = await SearchAsync(marker);
    Assert.AreEqual(1L, hits);
    var position = files.Single().Positions.Single();
    var expected = OffsetsText.IndexOf(marker, StringComparison.Ordinal);
    KnownBug.Expect(1, "positions are byte offsets", () => {
      Assert.AreEqual(expected, position.Position, "Position should be the UTF-16 offset of the match in the file");
      Assert.AreEqual(marker.Length, position.Length);
    });
  }

  [TestMethod]
  public async Task Positions_AsciiFile_AreUnchanged() {
    // Control: in an ASCII file, byte offsets and UTF-16 offsets are the same.
    var (_, files) = await SearchAsync(ChunkControlMarker);
    Assert.AreEqual(0, files.Single().Positions.Single().Position);
  }

  // --- #2: matches spanning chunk boundaries -------------------------------------------------

  [TestMethod]
  public async Task LargeFile_ControlMarker_IsFound() {
    var (hits, _) = await SearchAsync(ChunkControlMarker);
    Assert.AreEqual(1L, hits);
  }

  [TestMethod]
  public async Task LargeFile_MatchSpanningChunkBoundary_IsFound() {
    var (hits, files) = await SearchAsync(ChunkBoundaryMarker);
    KnownBug.Expect(2, "matches spanning 100 KB chunk boundaries are missed", () => {
      Assert.AreEqual(1L, hits);
      Assert.AreEqual(ChunkSize - 10, files.Single().Positions.Single().Position);
    });
  }

  // --- #3: non-ASCII patterns ----------------------------------------------------------------

  [TestMethod]
  public async Task NonAsciiPattern_MatchCase_IsFound() {
    var (hits, _) = await SearchAsync("検索テスト_NONASCII_PATTERN");
    KnownBug.Expect(3, "non-ASCII search patterns are mangled", () => Assert.AreEqual(1L, hits));
  }

  [TestMethod]
  public async Task NonAsciiPattern_IgnoreCase_IsFound() {
    var (hits, _) = await SearchAsync("über_nonascii_case", matchCase: false);
    KnownBug.Expect(3, "non-ASCII search patterns are mangled, case folding is ASCII only", () => Assert.AreEqual(1L, hits));
  }

  // --- #4: a single regex engine with the same results ----------------------------------------

  [TestMethod]
  public async Task Regex_Re2_DotMatchesNonAsciiCharacter() {
    // Already correct with RE2 (which runs on UTF-8 text): regression test.
    var (hits, _) = await SearchAsync("h.llo_regex_marker", regex: true, useRe2Engine: true);
    Assert.AreEqual(1L, hits);
  }

  [TestMethod]
  public async Task Regex_NonRe2_DotMatchesNonAsciiCharacter() {
    await KnownBug.ExpectAsync(4, "the non-RE2 regex engine (std::regex) runs on bytes", async () => {
      var (hits, _) = await SearchAsync("h.llo_regex_marker", regex: true, useRe2Engine: false);
      Assert.AreEqual(1L, hits);
    });
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task Regex_NonAsciiCharacterClass_IsFound(bool useRe2Engine) {
    await KnownBug.ExpectAsync(3, "non-ASCII regex patterns are mangled", async () => {
      var (hits, _) = await SearchOrFailAsync("[äöü]+_regex_class", regex: true, useRe2Engine: useRe2Engine);
      Assert.AreEqual(1L, hits);
    });
  }

  /// <summary>Same as <see cref="SearchAsync"/>, with server errors reported as assertion failures.</summary>
  private static async Task<(long HitCount, List<SearchResultFile> Files)> SearchOrFailAsync(string text, bool matchCase = true,
                                                                                           bool regex = false, bool useRe2Engine = true) {
    try {
      return await SearchAsync(text, matchCase, regex, useRe2Engine);
    } catch (ServerErrorException e) {
      Assert.Fail($"Server returned an error: {e.Message}");
      throw;
    }
  }
}
