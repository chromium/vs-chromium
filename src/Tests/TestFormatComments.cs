// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using VsChromium.Features.FormatComment;
using VsChromium.Tests.Mocks;

namespace VsChromium.Tests {
  [TestClass]
  public class TestFormatComments {
    [TestMethod]
    public void FormatCommentWorks() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  // Ut enim ad minim veniam, quis nostrud 
  //        exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, 
  //sunt in culpa qui officia deserunt mollit anim id est laborum.";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(3);
      var end = textSnapshot.GetLineFromLineNumber(6);
      var formatter = CreateCommentFormatter();
      var formatLines = formatter.FormatLines(CreateExtendSpanResult(start, end));

      Assert.AreEqual(2, formatLines.Indent);
      foreach (var line in formatLines.Lines) {
        Assert.IsTrue(line.Length + formatLines.Indent + 1 <= 80);
      }
      Assert.AreEqual(7, formatLines.Lines.Count);
      Assert.AreEqual("Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod", formatLines.Lines[0]);
      Assert.AreEqual("tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim", formatLines.Lines[1]);
      Assert.AreEqual("veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea", formatLines.Lines[2]);
      Assert.AreEqual("commodo consequat. Duis aute irure dolor in reprehenderit in voluptate", formatLines.Lines[3]);
      Assert.AreEqual("velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat", formatLines.Lines[4]);
      Assert.AreEqual("cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id", formatLines.Lines[5]);
      Assert.AreEqual("est laborum.", formatLines.Lines[6]);
    }

    [TestMethod]
    public void FormatCommentWorks2() {
      var sourceText = @"// Lorem   ipsum    ";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(0);
      var end = textSnapshot.GetLineFromLineNumber(0);
      var formatter = CreateCommentFormatter();
      var formatLines = formatter.FormatLines(CreateExtendSpanResult(start, end));

      Assert.AreEqual(0, formatLines.Indent);
      foreach (var line in formatLines.Lines) {
        Assert.IsTrue(line.Length + formatLines.Indent + 1 <= 80);
      }
      Assert.AreEqual(1, formatLines.Lines.Count);
      Assert.AreEqual("Lorem   ipsum", formatLines.Lines[0]);
    }

    [TestMethod]
    public void FormatCommentWorks3() {
      var sourceText = @"// ";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(0);
      var end = textSnapshot.GetLineFromLineNumber(0);
      var formatter = CreateCommentFormatter();
      var formatLines = formatter.FormatLines(CreateExtendSpanResult(start, end));

      Assert.AreEqual(0, formatLines.Indent);
      foreach (var line in formatLines.Lines) {
        Assert.IsTrue(line.Length + formatLines.Indent + 1 <= 80);
      }
      Assert.AreEqual(0, formatLines.Lines.Count);
    }

    [TestMethod]
    public void FormatCommentWorks4() {
      var sourceText = @"
#include <foo>

                          // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
                            // Ut enim ad minim veniam, quis nostrud 
                           //        exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, 
                              //sunt in culpa qui officia deserunt mollit anim id est laborum.";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(3);
      var end = textSnapshot.GetLineFromLineNumber(6);
      var formatter = CreateCommentFormatter();
      var formatLines = formatter.FormatLines(CreateExtendSpanResult(start, end));

      Assert.AreEqual(26, formatLines.Indent);
      foreach (var line in formatLines.Lines) {
        Assert.IsTrue(line.Length + formatLines.Indent + 1 <= 80);
      }
      Assert.AreEqual("Lorem ipsum dolor sit amet, consectetur adipisicing", formatLines.Lines[0]);
      Assert.AreEqual("elit, sed do eiusmod tempor incididunt ut labore et", formatLines.Lines[1]);
      Assert.AreEqual("dolore magna aliqua. Ut enim ad minim veniam, quis", formatLines.Lines[2]);
      Assert.AreEqual("nostrud exercitation ullamco laboris nisi ut", formatLines.Lines[3]);
      Assert.AreEqual("aliquip ex ea commodo consequat. Duis aute irure", formatLines.Lines[4]);
      Assert.AreEqual("dolor in reprehenderit in voluptate velit esse", formatLines.Lines[5]);
      Assert.AreEqual("cillum dolore eu fugiat nulla pariatur. Excepteur", formatLines.Lines[6]);
      Assert.AreEqual("sint occaecat cupidatat non proident, sunt in culpa", formatLines.Lines[7]);
      Assert.AreEqual("qui officia deserunt mollit anim id est laborum.", formatLines.Lines[8]);
      Assert.AreEqual(9, formatLines.Lines.Count);
    }

    [TestMethod]
    public void FormatCommentWorks5() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmo dfe";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(3);
      var end = textSnapshot.GetLineFromLineNumber(3);
      var formatter = CreateCommentFormatter();
      var formatLines = formatter.FormatLines(CreateExtendSpanResult(start, end));

      Assert.AreEqual(2, formatLines.Indent);
      foreach (var line in formatLines.Lines) {
        Assert.IsTrue(line.Length + formatLines.Indent + 1 <= 80);
      }
      Assert.AreEqual(1, formatLines.Lines.Count);
      Assert.AreEqual("Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmo dfe", formatLines.Lines[0]);
    }

    [TestMethod]
    public void FormatCommentWorks6() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmo dfef";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(3);
      var end = textSnapshot.GetLineFromLineNumber(3);
      var formatter = CreateCommentFormatter();
      var formatLines = formatter.FormatLines(CreateExtendSpanResult(start, end));

      Assert.AreEqual(2, formatLines.Indent);
      foreach (var line in formatLines.Lines) {
        Assert.IsTrue(line.Length + formatLines.Indent + 1 <= 80);
      }
      Assert.AreEqual(2, formatLines.Lines.Count);
      Assert.AreEqual("Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmo", formatLines.Lines[0]);
      Assert.AreEqual("dfef", formatLines.Lines[1]);
    }

    [TestMethod]
    public void ExtendSpanWorks() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  // Ut enim ad minim veniam, quis nostrud 
  //        exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, 
  //sunt in culpa qui officia deserunt mollit anim id est laborum.";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(3);
      var formatter = CreateCommentFormatter();
      var result = formatter.ExtendSpan(new SnapshotSpan(start.Start, start.Start));

      Assert.IsNotNull(result);
      Assert.AreEqual(3, result.StartLine.LineNumber);
      Assert.AreEqual(6, result.EndLine.LineNumber);
    }

    [TestMethod]
    public void ExtendSpanWorks2() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  // Ut enim ad minim veniam, quis nostrud 
  //        exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, 
  //sunt in culpa qui officia deserunt mollit anim id est laborum.";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(3);
      var end = textSnapshot.GetLineFromLineNumber(6);
      var formatter = CreateCommentFormatter();
      var result = formatter.ExtendSpan(new SnapshotSpan(start.Start, end.End));

      Assert.IsNotNull(result);
      Assert.AreEqual(3, result.StartLine.LineNumber);
      Assert.AreEqual(6, result.EndLine.LineNumber);
    }

    [TestMethod]
    public void ExtendSpanWorks3() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
   Ut enim ad minim veniam, quis nostrud 
  //        exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, 
  //sunt in culpa qui officia deserunt mollit anim id est laborum.";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(3);
      var end = textSnapshot.GetLineFromLineNumber(6);
      var formatter = CreateCommentFormatter();
      var result = formatter.ExtendSpan(new SnapshotSpan(start.Start, end.End));

      Assert.IsNull(result);
    }

    [TestMethod]
    public void ExtendSpanWorks4() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  // Ut enim ad minim veniam, quis nostrud 
  //        exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, 
  //sunt in culpa qui officia deserunt mollit anim id est laborum.
";

      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var start = textSnapshot.GetLineFromLineNumber(3);
      var end = textSnapshot.GetLineFromLineNumber(7);
      var formatter = CreateCommentFormatter();
      var result = formatter.ExtendSpan(new SnapshotSpan(start.Start, end.Start));

      Assert.IsNotNull(result);
      Assert.AreEqual(3, result.StartLine.LineNumber);
      Assert.AreEqual(6, result.EndLine.LineNumber);
    }


    [TestMethod]
    public void ApplyChangesWorks() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmo dfef";

      var expectedEndText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmo
  // dfef";

      CheckCommentFormatting(sourceText, expectedEndText);
    }

    [TestMethod]
    public void ApplyChangesWorks2() {
      var sourceText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmo dfe";

      var expectedEndText = @"
#include <foo>

  // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmo dfe";

      CheckCommentFormatting(sourceText, expectedEndText);
    }

    [TestMethod]
    public void ApplyChangesWorks3() {
      var sourceText = @"
#include <foo>

                          // Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
                            // Ut enim ad minim veniam, quis nostrud 
                           //        exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, 
                              //sunt in culpa qui officia deserunt mollit anim id est laborum.";

      var expectedEndText = @"
#include <foo>

                          // Lorem ipsum dolor sit amet, consectetur adipisicing
                          // elit, sed do eiusmod tempor incididunt ut labore et
                          // dolore magna aliqua. Ut enim ad minim veniam, quis
                          // nostrud exercitation ullamco laboris nisi ut
                          // aliquip ex ea commodo consequat. Duis aute irure
                          // dolor in reprehenderit in voluptate velit esse
                          // cillum dolore eu fugiat nulla pariatur. Excepteur
                          // sint occaecat cupidatat non proident, sunt in culpa
                          // qui officia deserunt mollit anim id est laborum.";

      CheckCommentFormatting(sourceText, expectedEndText);
    }

    [TestMethod]
    public void ApplyChangesForTripleSlashCommentsWorks() {
      var sourceText = @"
#include <foo>

                         /// Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
                            /// Ut enim ad minim veniam, quis nostrud 
                           ///        exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, 
                              ///sunt in culpa qui officia deserunt mollit anim id est laborum.";

      var expectedEndText = @"
#include <foo>

                         /// Lorem ipsum dolor sit amet, consectetur adipisicing
                         /// elit, sed do eiusmod tempor incididunt ut labore et
                         /// dolore magna aliqua. Ut enim ad minim veniam, quis
                         /// nostrud exercitation ullamco laboris nisi ut
                         /// aliquip ex ea commodo consequat. Duis aute irure
                         /// dolor in reprehenderit in voluptate velit esse
                         /// cillum dolore eu fugiat nulla pariatur. Excepteur
                         /// sint occaecat cupidatat non proident, sunt in culpa
                         /// qui officia deserunt mollit anim id est laborum.";

      CheckCommentFormatting(sourceText, expectedEndText);
    }

    private void CheckCommentFormatting(string sourceText, string expectedEndText) {
      var textBuffer = CreateTestBuffer(sourceText);
      var textSnapshot = textBuffer.CurrentSnapshot;
      var textEdit = textBuffer.CreateEdit();
      var start = textSnapshot.GetLineFromLineNumber(3);
      var formatter = CreateCommentFormatter();
      var extendSpanResult = formatter.ExtendSpan(new SnapshotSpan(start.Start, start.Start));
      var formatLines = formatter.FormatLines(extendSpanResult);
      formatter.ApplyChanges(textEdit, formatLines);
      var newSnapshot = textEdit.Apply();

      var expectedSnapshot = CreateTestBuffer(expectedEndText).CurrentSnapshot;
      var e1 = expectedSnapshot.Lines.GetEnumerator();
      var e2 = newSnapshot.Lines.GetEnumerator();
      while (true) {
        if (e1.MoveNext()) {
          Assert.IsTrue(e2.MoveNext());
          Assert.AreEqual(e1.Current.GetTextIncludingLineBreak(), e2.Current.GetTextIncludingLineBreak());
        } else {
          Assert.IsFalse(e2.MoveNext());
          break;
        }
      }
    }

    private static void GenerateAsserts(FormatLinesResult formatLines) {
      Trace.WriteLine(string.Format("Assert.AreEqual({0}, formatLines.Indent);", formatLines.Indent));
      Trace.WriteLine(string.Format("Assert.AreEqual({0}, formatLines.Lines.Count);", formatLines.Lines.Count));
      int lineNumber = 0;
      foreach (var line in formatLines.Lines) {
        Trace.WriteLine(string.Format("Assert.AreEqual(\"{0}\", formatLines.Lines[{1}]);", line, lineNumber));
        lineNumber++;
      }
    }

    private ITextBuffer CreateTestBuffer(string text) {
      return new TextBufferMock(text);
    }

    private ExtendSpanResult CreateExtendSpanResult(ITextSnapshotLine start, ITextSnapshotLine end) {
      return new ExtendSpanResult {
        CommentType = new CommentType("//"),
        StartLine = start,
        EndLine = end,
      };
    }

    private static ICommentFormatter CreateCommentFormatter() {
      return new CommentFormatter();
    }
  }
}
