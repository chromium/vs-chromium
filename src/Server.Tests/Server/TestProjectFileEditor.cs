// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Files.PatternMatching;
using VsChromium.Core.Utility;
using VsChromium.Server.Projects;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestProjectFileEditor {
    [TestMethod]
    public void BuildExactPatternAnchorsAtRootAndDistinguishesDirectoriesFromFiles() {
      Assert.AreEqual("/out/", ProjectFileEditor.BuildExactPattern("", "out", isDirectory: true));
      Assert.AreEqual("/src/build/Debug/", ProjectFileEditor.BuildExactPattern("src/build", "Debug", isDirectory: true));
      Assert.AreEqual("/src/build/Debug/", ProjectFileEditor.BuildExactPattern("src\\build", "Debug", isDirectory: true));
      Assert.AreEqual("/big.bin", ProjectFileEditor.BuildExactPattern("", "big.bin", isDirectory: false));
      Assert.AreEqual("/data/models/weights.bin", ProjectFileEditor.BuildExactPattern("data/models", "weights.bin", isDirectory: false));

      Assert.IsNull(ProjectFileEditor.BuildExactPattern("", "wild*name", isDirectory: false));
      Assert.IsNull(ProjectFileEditor.BuildExactPattern("dir*1", "file.txt", isDirectory: false));
      Assert.IsNull(ProjectFileEditor.BuildExactPattern("", "", isDirectory: true));

      var dirPattern = ProjectFileEditor.BuildExactPattern("out", "Debug", isDirectory: true)!
        .Replace('/', Path.DirectorySeparatorChar);
      var dirMatcher = PatternParser.ParsePattern(dirPattern);
      var comparer = SystemPathComparer.Instance;

      Assert.IsTrue(dirMatcher.MatchDirectoryName(JoinRel("out", "Debug").AsSpan(), comparer));
      Assert.IsTrue(dirMatcher.MatchFileName(JoinRel("out", "Debug", "foo.obj").AsSpan(), comparer));
      Assert.IsFalse(dirMatcher.MatchFileName(JoinRel("out", "Debug").AsSpan(), comparer));
      Assert.IsFalse(dirMatcher.MatchDirectoryName(JoinRel("src", "out", "Debug").AsSpan(), comparer));
      Assert.IsFalse(dirMatcher.MatchDirectoryName(JoinRel("out", "Debug2").AsSpan(), comparer));

      var filePattern = ProjectFileEditor.BuildExactPattern("data", "big.bin", isDirectory: false)!
        .Replace('/', Path.DirectorySeparatorChar);
      var fileMatcher = PatternParser.ParsePattern(filePattern);
      Assert.IsTrue(fileMatcher.MatchFileName(JoinRel("data", "big.bin").AsSpan(), comparer));
      Assert.IsFalse(fileMatcher.MatchFileName(JoinRel("src", "data", "big.bin").AsSpan(), comparer));
      Assert.IsFalse(fileMatcher.MatchFileName(JoinRel("data", "big.bin2").AsSpan(), comparer));
    }

    [TestMethod]
    public void EditProjectFileBytesInsertsIntoExistingSectionPreservingCommentsBlankLinesAndCrlf() {
      const string original =
        "# Header comment\r\n" +
        "[SourceExplorer.ignore]\r\n" +
        "# Keep git hidden\r\n" +
        ".git/\r\n" +
        "\r\n" +
        "[SearchableFiles.ignore]\r\n" +
        "*.png\r\n";

      var input = Encoding.UTF8.GetBytes(original);
      var output = ProjectFileEditor.EditProjectFileBytes(
        input,
        ConfigurationSectionNames.SourceExplorerIgnore,
        "/out/Debug/",
        out var added,
        out var line);

      Assert.IsTrue(added);
      Assert.AreEqual("/out/Debug/", line);
      var updated = Encoding.UTF8.GetString(output);
      const string expected =
        "# Header comment\r\n" +
        "[SourceExplorer.ignore]\r\n" +
        "# Keep git hidden\r\n" +
        ".git/\r\n" +
        "/out/Debug/\r\n" +
        "\r\n" +
        "[SearchableFiles.ignore]\r\n" +
        "*.png\r\n";
      Assert.AreEqual(expected, updated);
    }

    [TestMethod]
    public void EditProjectFileBytesPreservesLfLineEndings() {
      const string original = "[SourceExplorer.ignore]\n.git/\n\n[SearchableFiles.include]\n*\n";
      var input = Encoding.UTF8.GetBytes(original);
      var output = ProjectFileEditor.EditProjectFileBytes(
        input,
        ConfigurationSectionNames.SearchableFilesIgnore,
        "/data/huge.json",
        out var added,
        out _);

      Assert.IsTrue(added);
      var updated = Encoding.UTF8.GetString(output);
      Assert.IsFalse(updated.Contains('\r'), "Must keep LF-only line endings");
      Assert.AreEqual(
        "[SourceExplorer.ignore]\n.git/\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n/data/huge.json\n",
        updated);
    }

    [TestMethod]
    public void EditProjectFileBytesPreservesUtf8BomAndUtf16LeBom() {
      var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
      var originalText = "[SearchableFiles.ignore]\r\n*.dll\r\n";
      var utf8Bytes = Concat(utf8Bom.GetPreamble(), utf8Bom.GetBytes(originalText));

      var updatedUtf8 = ProjectFileEditor.EditProjectFileBytes(
        utf8Bytes,
        ConfigurationSectionNames.SearchableFilesIgnore,
        "/third_party/blobs/",
        out var addedUtf8,
        out _);
      Assert.IsTrue(addedUtf8);
      Assert.AreEqual(0xEF, updatedUtf8[0]);
      Assert.AreEqual(0xBB, updatedUtf8[1]);
      Assert.AreEqual(0xBF, updatedUtf8[2]);
      Assert.AreEqual(
        "[SearchableFiles.ignore]\r\n*.dll\r\n/third_party/blobs/\r\n",
        utf8Bom.GetString(updatedUtf8, 3, updatedUtf8.Length - 3));

      var utf16Le = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
      var utf16Bytes = Concat(utf16Le.GetPreamble(), utf16Le.GetBytes(originalText));
      var updatedUtf16 = ProjectFileEditor.EditProjectFileBytes(
        utf16Bytes,
        ConfigurationSectionNames.SearchableFilesIgnore,
        "/third_party/blobs/",
        out var addedUtf16,
        out _);
      Assert.IsTrue(addedUtf16);
      Assert.AreEqual(0xFF, updatedUtf16[0]);
      Assert.AreEqual(0xFE, updatedUtf16[1]);
      Assert.AreEqual(
        "[SearchableFiles.ignore]\r\n*.dll\r\n/third_party/blobs/\r\n",
        utf16Le.GetString(updatedUtf16, 2, updatedUtf16.Length - 2));
    }

    [TestMethod]
    public void EditProjectFileBytesSkipsDuplicatePatternsAndHandlesEmptySectionAndMissingTrailingNewline() {
      const string withDuplicate = "[SourceExplorer.ignore]\r\n\\out\\Debug\\\r\n";
      var dupInput = Encoding.UTF8.GetBytes(withDuplicate);
      var dupOutput = ProjectFileEditor.EditProjectFileBytes(
        dupInput,
        ConfigurationSectionNames.SourceExplorerIgnore,
        "/out/Debug/",
        out var added,
        out var existingLine);
      Assert.IsFalse(added);
      Assert.AreEqual("\\out\\Debug\\", existingLine);
      CollectionAssert.AreEqual(dupInput, dupOutput);

      // Empty section followed by another section.
      const string emptySection = "[SourceExplorer.ignore]\r\n\r\n[SearchableFiles.include]\r\n*";
      var outEmpty = ProjectFileEditor.EditProjectFileBytes(
        Encoding.UTF8.GetBytes(emptySection),
        ConfigurationSectionNames.SourceExplorerIgnore,
        "/build/",
        out var addedEmpty,
        out _);
      Assert.IsTrue(addedEmpty);
      Assert.AreEqual(
        "[SourceExplorer.ignore]\r\n/build/\r\n\r\n[SearchableFiles.include]\r\n*",
        Encoding.UTF8.GetString(outEmpty));

      // File without trailing newline at EOF.
      const string noFinalNewline = "[SearchableFiles.ignore]\r\n*.pdb";
      var outNoNewline = ProjectFileEditor.EditProjectFileBytes(
        Encoding.UTF8.GetBytes(noFinalNewline),
        ConfigurationSectionNames.SearchableFilesIgnore,
        "/out/",
        out var addedNoNewline,
        out _);
      Assert.IsTrue(addedNoNewline);
      Assert.AreEqual("[SearchableFiles.ignore]\r\n*.pdb\r\n/out/\r\n", Encoding.UTF8.GetString(outNoNewline));
    }

    [TestMethod]
    public void ValidatePatternRejectsEmptyCommentsAndSectionHeaders() {
      Assert.ThrowsExactly<RecoverableErrorException>(() => ProjectFileEditor.ValidatePattern(""));
      Assert.ThrowsExactly<RecoverableErrorException>(() => ProjectFileEditor.ValidatePattern("   "));
      Assert.ThrowsExactly<RecoverableErrorException>(() => ProjectFileEditor.ValidatePattern("# comment"));
      Assert.ThrowsExactly<RecoverableErrorException>(() => ProjectFileEditor.ValidatePattern("[SourceExplorer.ignore]"));
      Assert.ThrowsExactly<RecoverableErrorException>(() => ProjectFileEditor.ValidatePattern("a\nb"));
    }

    private static string JoinRel(params string[] parts) {
      return string.Join(Path.DirectorySeparatorChar, parts);
    }

    private static byte[] Concat(byte[] a, byte[] b) {
      var res = new byte[a.Length + b.Length];
      Buffer.BlockCopy(a, 0, res, 0, a.Length);
      Buffer.BlockCopy(b, 0, res, a.Length, b.Length);
      return res;
    }
  }
}
