// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;
using VsChromium.Tests.EndToEnd.Corpus;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// Regression tests for the behavior changes of the port (bugs of the native server), see
/// "Behavior changes" in docs/portable-server/PORTING_PRINCIPLES.md. Each test describes the correct
/// behavior. Unless noted otherwise ("control" and "regression" tests), the tests failed with the
/// native server (before milestone 4 of the port). Use <see cref="KnownBug"/> for tests of bugs not
/// fixed yet.
/// </summary>
[TestClass]
public class BehaviorChangeTests {
  private static JsonRpcServerClient? s_server;
  private static string s_root = "";

  private static JsonRpcServerClient Server => s_server ?? throw new InvalidOperationException("Server not started");

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
  /// <summary>Chunk size of the server (FileDatabaseBuilder.ChunkSize).</summary>
  private const int ChunkSize = 100 * 1024;

  private const string NonAsciiFile = "non_ascii_pattern.txt";
  private const string NonAsciiText =
    "line with 検索テスト_NONASCII_PATTERN in it\n" +
    "line with ÜBER_NONASCII_CASE in it\n" +
    "line with héllo_regex_marker in it\n" +
    "line with äöü_regex_class in it\n";

  private const string AnchorsFile = "regex_anchors.txt";
  private const string AnchorsText =
    "ANCHOR_MARKER at line start\n" +
    "ANCHOR_MARKER at line start again\n" +
    "  not at line start: ANCHOR_MARKER\n" +
    "ANCHOR_MARKER\n";

  private const string WholeWordFile = "whole_word.txt";
  private const string WholeWordText =
    "éWW_MARKER WW_MARKER\n";

  private const string Latin1File = "latin1.txt";
  // "café LATIN1_MARKER" with "é" encoded as a single (invalid UTF-8) byte.
  private static readonly byte[] Latin1Bytes =
    [.. Encoding.ASCII.GetBytes("caf"), 0xE9, .. Encoding.ASCII.GetBytes(" LATIN1_MARKER\n")];

  private const string NonAsciiFileName = "日本_path_marker.txt";

  private const string Utf16LittleEndianFile = "utf16le.txt";
  private const string Utf16BigEndianFile = "utf16be.txt";
  private const string Utf16Marker = "UTF16_FILE_MARKER";
  private const string Utf16Text = "first line\r\nemoji 🎉 then UTF16_FILE_MARKER here\r\n";

  // NUL byte in the first 8,000 bytes: binary (git rule).
  private const string NulPrefixFile = "nul_prefix.txt";
  private const string NulPrefixMarker = "NUL_PREFIX_MARKER";
  // NUL byte after the first 8,000 bytes: text.
  private const string NulLateFile = "nul_late.txt";
  private const string NulLateMarker = "NUL_LATE_MARKER";
  // Many control characters (more than 10% of the bytes, the former heuristic) but no NUL: text.
  private const string ControlCharsFile = "control_chars.txt";
  private const string ControlCharsMarker = "CONTROL_CHARS_MARKER";

  // A file whose full path is longer than MAX_PATH (260 characters) on any machine.
  private static readonly string LongPathRelativeDirectory = Path.Combine(
    new string('a', 60), new string('b', 60), new string('c', 60), new string('d', 60));
  private const string LongPathMarker = "LONG_PATH_MARKER";

  private static void GenerateFiles(string root) {
    File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"),
      "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
    var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    File.WriteAllText(Path.Combine(root, OffsetsFile), OffsetsText, utf8);
    File.WriteAllText(Path.Combine(root, NonAsciiFile), NonAsciiText, utf8);
    File.WriteAllText(Path.Combine(root, AnchorsFile), AnchorsText, utf8);
    File.WriteAllText(Path.Combine(root, WholeWordFile), WholeWordText, utf8);
    File.WriteAllBytes(Path.Combine(root, Latin1File), Latin1Bytes);
    File.WriteAllText(Path.Combine(root, NonAsciiFileName), "contents\n", utf8);
    File.WriteAllText(Path.Combine(root, Utf16LittleEndianFile), Utf16Text, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));
    File.WriteAllText(Path.Combine(root, Utf16BigEndianFile), Utf16Text, new UnicodeEncoding(bigEndian: true, byteOrderMark: true));
    File.WriteAllBytes(Path.Combine(root, NulPrefixFile), [.. Encoding.ASCII.GetBytes("text"), 0, .. Encoding.ASCII.GetBytes(" " + NulPrefixMarker + "\n")]);
    File.WriteAllBytes(Path.Combine(root, NulLateFile),
      [.. Encoding.ASCII.GetBytes(new string('x', 8100) + "\n"), 0, .. Encoding.ASCII.GetBytes(" " + NulLateMarker + "\n")]);
    File.WriteAllBytes(Path.Combine(root, ControlCharsFile),
      [.. Enumerable.Repeat(Encoding.ASCII.GetBytes("\u001b[31m\u001b[0m\f\b"), 20).SelectMany(x => x), .. Encoding.ASCII.GetBytes(ControlCharsMarker + "\n")]);
    var longPathDirectory = Path.Combine(root, LongPathRelativeDirectory);
    Directory.CreateDirectory(longPathDirectory);
    File.WriteAllText(Path.Combine(longPathDirectory, "long_path_file.txt"), LongPathMarker + "\n", utf8);

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
    s_root = CorpusCache.GetOrCreateCustom("behavior-changes", version: 5, GenerateFiles);
    s_server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
    await Server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
      () => Server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(s_root, "vs-chromium-project.txt"))));
  }

  [ClassCleanup]
  public static async Task ClassCleanup() {
    if (s_server != null) {
      await s_server.DisposeAsync();
    }
  }

  private static async Task<(long HitCount, List<SearchResultFile> Files)> SearchAsync(string text, bool matchCase = true,
                                                                                     bool regex = false, bool matchWholeWord = false) {
    var response = await Server.RequestAsync(ServerProtocol.SearchCode(text, matchCase: matchCase, regex: regex,
      matchWholeWord: matchWholeWord));
    return (response.Data["hitCount"]!.GetValue<long>(), ServerProtocol.FlattenCodeResults(response.Data));
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
    Assert.AreEqual(expected, position.Position, "Position should be the UTF-16 offset of the match in the file");
    Assert.AreEqual(marker.Length, position.Length);
  }

  [TestMethod]
  [DataRow("OFFSET_MARKER_LATIN")]
  [DataRow("OFFSET_MARKER_EMOJI")]
  public async Task FileExtracts_AreConsistentWithUtf16Offsets(string marker) {
    var (_, files) = await SearchAsync(marker);
    var file = files.Single();
    var response = await Server.RequestAsync(ServerProtocol.GetFileExtracts(file.FullPath, file.Positions));
    var extract = response.Data["extracts"]!.AsArray().Single()!;
    var text = (string)extract["text"]!;
    var offset = extract["offset"]!.GetValue<int>();
    var expectedLine = OffsetsText.Split('\n').Single(x => x.Contains(marker));
    StringAssert.Contains(text, marker);
    Assert.AreEqual(OffsetsText.IndexOf(expectedLine, StringComparison.Ordinal), offset);
    Assert.AreEqual(expectedLine.IndexOf(marker, StringComparison.Ordinal), extract["columnNumber"]!.GetValue<int>());
  }

  [TestMethod]
  public async Task Positions_AsciiFile_AreUnchanged() {
    // Control: in an ASCII file, byte offsets and UTF-16 offsets are the same.
    var (_, files) = await SearchAsync(ChunkControlMarker);
    Assert.AreEqual(0, files.Single().Positions.Single().Position);
  }

  [TestMethod]
  public async Task Positions_InvalidUtf8File_AreCharacterOffsets() {
    // Control: a file with invalid UTF-8 bytes (e.g. Latin-1) is decoded as UTF-8 with replacement characters:
    // one character per invalid byte, so that offsets are the same as in Latin-1.
    var (hits, files) = await SearchAsync("LATIN1_MARKER");
    Assert.AreEqual(1L, hits);
    Assert.AreEqual(5, files.Single().Positions.Single().Position);
  }

  // --- #2: matches spanning chunk boundaries -------------------------------------------------

  [TestMethod]
  public async Task LargeFile_ControlMarker_IsFound() {
    // Control
    var (hits, _) = await SearchAsync(ChunkControlMarker);
    Assert.AreEqual(1L, hits);
  }

  [TestMethod]
  public async Task LargeFile_MatchSpanningChunkBoundary_IsFound() {
    var (hits, files) = await SearchAsync(ChunkBoundaryMarker);
    Assert.AreEqual(1L, hits);
    Assert.AreEqual(ChunkSize - 10, files.Single().Positions.Single().Position);
  }

  // --- #3: non-ASCII patterns ----------------------------------------------------------------

  [TestMethod]
  public async Task NonAsciiPattern_MatchCase_IsFound() {
    var (hits, _) = await SearchAsync("検索テスト_NONASCII_PATTERN");
    Assert.AreEqual(1L, hits);
  }

  [TestMethod]
  public async Task NonAsciiPattern_IgnoreCase_IsFound() {
    var (hits, _) = await SearchAsync("über_nonascii_case", matchCase: false);
    Assert.AreEqual(1L, hits);
  }

  // --- #4: a single regex engine with the same results ----------------------------------------
  // (The "useRe2Engine" parameter of the typed messages protocol is gone: there is a single regex
  // engine.)

  [TestMethod]
  public async Task Regex_DotMatchesNonAsciiCharacter() {
    // Regression test: already correct with RE2 (which runs on UTF-8 text).
    var (hits, _) = await SearchAsync("h.llo_regex_marker", regex: true);
    Assert.AreEqual(1L, hits);
  }

  [TestMethod]
  public async Task Regex_NonAsciiCharacterClass_IsFound() {
    var (hits, _) = await SearchOrFailAsync("[äöü]+_regex_class", regex: true);
    Assert.AreEqual(1L, hits);
  }

  // --- #5: regex line anchors ------------------------------------------------------------------

  [TestMethod]
  public async Task Regex_LineStartAnchor_MatchesAtEveryLineStart() {
    var (hits, files) = await SearchAsync("^ANCHOR_MARKER", regex: true);
    Assert.AreEqual(3L, hits);
    var lineStarts = new[] { 0, AnchorsText.IndexOf("ANCHOR_MARKER at line start again", StringComparison.Ordinal),
                             AnchorsText.LastIndexOf("ANCHOR_MARKER", StringComparison.Ordinal) };
    CollectionAssert.AreEqual(lineStarts, files.Single().Positions.Select(x => x.Position).ToArray());
  }

  [TestMethod]
  public async Task Regex_LineEndAnchor_MatchesAtEveryLineEnd() {
    var (hits, _) = await SearchAsync("ANCHOR_MARKER$", regex: true);
    Assert.AreEqual(2L, hits);
  }

  // --- #6: whole word in non-ASCII text ------------------------------------------------------

  [TestMethod]
  public async Task WholeWord_NonAsciiLetter_IsWordCharacter() {
    var (hits, files) = await SearchAsync("WW_MARKER", matchWholeWord: true);
    Assert.AreEqual(1L, hits);
    Assert.AreEqual(WholeWordText.LastIndexOf("WW_MARKER", StringComparison.Ordinal), files.Single().Positions.Single().Position);
  }

  // --- #8: regex file path search with non-ASCII paths ---------------------------------------

  [TestMethod]
  public async Task Regex_FilePathSearch_NonAsciiFileName_IsFound() {
    var response = await Server.RequestAsync(ServerProtocol.SearchFilePaths("日本_path_marker", regex: true));
    Assert.AreEqual(1L, response.Data["hitCount"]!.GetValue<long>());
  }

  // --- #9: paths longer than MAX_PATH --------------------------------------------------------

  [TestMethod]
  public async Task LongPath_FileIsIndexed() {
    var (hits, files) = await SearchAsync(LongPathMarker);
    Assert.AreEqual(1L, hits);
    Assert.IsTrue(files.Single().FullPath.Length > 260, files.Single().FullPath);
  }

  // --- #14: binary file detection (git rule) and UTF-16 files -------------------------------

  [TestMethod]
  [DataRow(Utf16LittleEndianFile)]
  [DataRow(Utf16BigEndianFile)]
  public async Task Utf16FileWithBom_IsSearchable(string fileName) {
    var (hits, files) = await SearchAsync(Utf16Marker);
    var file = files.SingleOrDefault(f => f.FullPath.EndsWith(fileName, StringComparison.Ordinal));
    Assert.IsNotNull(file, $"{fileName} should be searchable ({hits} hits)");
    // UTF-16 positions in the decoded text.
    Assert.AreEqual(Utf16Text.IndexOf(Utf16Marker, StringComparison.Ordinal), file.Positions.Single().Position);
  }

  [TestMethod]
  public async Task NulByteInFirst8000Bytes_FileIsBinary() {
    var (hits, _) = await SearchAsync(NulPrefixMarker);
    Assert.AreEqual(0L, hits, "A file with a NUL byte in its first 8,000 bytes is binary (as git)");
  }

  [TestMethod]
  public async Task NulByteAfterFirst8000Bytes_FileIsText() {
    var (hits, _) = await SearchAsync(NulLateMarker);
    Assert.AreEqual(1L, hits);
  }

  [TestMethod]
  public async Task ControlCharactersWithoutNul_FileIsText() {
    var (hits, _) = await SearchAsync(ControlCharsMarker);
    Assert.AreEqual(1L, hits, "Files with many control characters but no NUL byte are text (formerly binary)");
  }

  /// <summary>Same as <see cref="SearchAsync"/>, with server errors reported as assertion failures.</summary>
  private static async Task<(long HitCount, List<SearchResultFile> Files)> SearchOrFailAsync(string text, bool matchCase = true,
                                                                                           bool regex = false) {
    try {
      return await SearchAsync(text, matchCase, regex);
    } catch (ServerErrorException e) {
      Assert.Fail($"Server returned an error: {e.Message}");
      throw;
    }
  }
}
