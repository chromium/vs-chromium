// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;

namespace VsChromium.Features.FormatComment {
  [Export(typeof(ICommentFormatter))]
  public class CommentFormatter : ICommentFormatter {
    private const string _defaultLineEnding = "\n";
    private const int _maxColumn = 80;
    private const string _shortCommentToken = "//";
    private const string _longCommentToken = "///";

    public ExtendSpanResult ExtendSpan(SnapshotSpan span) {
      // Adjust end of span if 1) selection and 2) end of span is at start of line
      if (span.Length > 0) {
        var endLine1 = span.End.GetContainingLine();
        if (endLine1.LineNumber > 0 && span.End == endLine1.Start) {
          span = new SnapshotSpan(span.Start, span.Snapshot.GetLineFromLineNumber(endLine1.LineNumber - 1).End);
        }
      }

      // If no selection, extend lines up and down.
      var deltaLine = (span.Length == 0 ? 1 : 0);

      var startLine = FindCommentLine(span, span.Start.Position, -deltaLine);
      if (startLine == null)
        return null;

      var endLine = FindCommentLine(span, span.End.Position, deltaLine);
      if (endLine == null)
        return null;

      // Check all lines are comments lines
      CommentType commentType = null;
      for (var i = startLine.LineNumber; i <= endLine.LineNumber; i++) {
        var lineCommentType = GetCommentType(span.Snapshot.GetLineFromLineNumber(i));
        if (lineCommentType == null)
          return null;

        if (commentType == null) {
          commentType = lineCommentType;
          continue;
        }

        if (commentType.Token != lineCommentType.Token)
          return null;
      }

      return new ExtendSpanResult {
        CommentType = commentType,
        StartLine = startLine,
        EndLine = endLine,
      };
    }

    public FormatLinesResult FormatLines(ExtendSpanResult span) {
      var indent = GetCommentBlockIndent(span);
      if (indent < 0)
        throw new ArgumentException("Line range does not contains a block comment.");

      var commentText = GetCommentBlockText(span);

      return new FormatLinesResult {
        CommentType = span.CommentType,
        SnapshotSpan = new SnapshotSpan(span.StartLine.Snapshot, new Span(span.StartLine.Start, span.EndLine.End - span.StartLine.Start)),
        Indent = indent,
        Lines = FormatCommentText(commentText, _maxColumn - span.CommentType.TextPrefix.Length - indent).ToList()
      };
    }

    public bool ApplyChanges(ITextEdit textEdit, FormatLinesResult result) {
      var lineEnding = DetectLineEnding(textEdit.Snapshot);

      var sb = new StringBuilder();
      var indent = new string(' ', result.Indent);
      foreach (var line in result.Lines) {
        if (sb.Length > 0)
          sb.Append(lineEnding);
        sb.Append(indent);
        sb.Append(result.CommentType.TextPrefix);
        sb.Append(line);
      }
      var commentText = sb.ToString();
      var oldSpan = result.SnapshotSpan.Span;
      var oldText = textEdit.Snapshot.GetText(oldSpan);
      if (oldText == commentText)
        return false;

      textEdit.Replace(oldSpan, commentText);
      return true;
    }

    private string DetectLineEnding(ITextSnapshot snapshot) {
      var table = new Dictionary<string, int>();
      foreach (var line in snapshot.Lines) {
        var ending = line.GetLineBreakText();
        if (table.ContainsKey(ending)) {
          table[ending] = table[ending] + 1;
        } else {
          table[ending] = 1;
        }
      }

      if (table.Count == 0) {
        return _defaultLineEnding;
      }

      return table.OrderByDescending(x => x.Value).Select(x => x.Key).First();
    }

    private bool IsCommentLine(ITextSnapshotLine line) {
      return GetCommentType(line) != null;
    }

    private CommentType GetCommentType(ITextSnapshotLine line) {
      var text = line.GetText().Trim();
      if (text.StartsWith(_longCommentToken))
        return new CommentType(_longCommentToken);
      if (text.StartsWith(_shortCommentToken))
        return new CommentType(_shortCommentToken);
      return null;
    }

    private ITextSnapshotLine FindCommentLine(SnapshotSpan span, int position, int delta) {
      var line = span.Snapshot.GetLineFromPosition(position);
      if (!IsCommentLine(line))
        return null;

      if (delta != 0) {
        while (line.LineNumber >= 0 && line.LineNumber < span.Snapshot.LineCount - 1) {
          var nextLine = span.Snapshot.GetLineFromLineNumber(line.LineNumber + delta);
          if (!IsCommentLine(nextLine))
            break;

          line = nextLine;
        }
      }

      return line;
    }

    public class CommentData {
      public SnapshotSpan SnapshotSpan { get; set; }
      public IList<string> Lines { get; set; }
    }

    private IEnumerable<string> FormatCommentText(string commentText, int maxLineLength) {
      var index = 0;
      while (index < commentText.Length) {
        var lineData = ExtractLine(commentText, index, maxLineLength);
        if (lineData.Length > 0)
          yield return commentText.Substring(lineData.StartIndex, lineData.Length);
        index = lineData.NewIndex;
      }
    }

    private struct LineData {
      private readonly int _startIndex;
      private readonly int _length;
      private readonly int _newIndex;

      public LineData(int startIndex, int length, int newIndex) {
        _startIndex = startIndex;
        _length = length;
        _newIndex = newIndex;
      }

      public int StartIndex { get { return _startIndex; } }

      public int Length { get { return _length; } }

      public int NewIndex { get { return _newIndex; } }
    }

    private LineData ExtractLine(string text, int startIndex, int lineLength) {
      Debug.Assert(startIndex >= 0);
      Debug.Assert(startIndex <= text.Length);
      Debug.Assert(lineLength > 0);

      // Skip initial whitespaces
      for (; startIndex < text.Length; startIndex++) {
        if (!char.IsWhiteSpace(text[startIndex]))
          break;
      }

      if (startIndex == text.Length)
        return new LineData(startIndex, 0, startIndex);

      var maxStartIndex = startIndex + lineLength - 1;
      var inWord = true;
      var firstWordIndex = startIndex;
      var lastWordEndIndex = -1;
      while (true) {
        // We reached end of string
        if (startIndex >= text.Length) {
          if (inWord) {
            lastWordEndIndex = startIndex - 1;
          }
          var length = lastWordEndIndex >= 0 ? lastWordEndIndex + 1 - firstWordIndex : startIndex - firstWordIndex;
          return new LineData(firstWordIndex, length, startIndex);
        }

        // We reached a whitespace
        if (char.IsWhiteSpace(text[startIndex])) {
          if (inWord) {
            lastWordEndIndex = startIndex - 1;
          }
          inWord = false;
        } else {
          // We reached a non whitespace
          inWord = true;
        }

        // We reached end of line
        if (startIndex > maxStartIndex && lastWordEndIndex >= 0) {
          var length = lastWordEndIndex + 1 - firstWordIndex;
          return new LineData(firstWordIndex, length, lastWordEndIndex + 1);
        }

        startIndex++;
      }
    }

    private string GetCommentBlockText(ExtendSpanResult span) {
      var sb = new StringBuilder();
      for (var i = span.StartLine.LineNumber; i <= span.EndLine.LineNumber; i++) {
        var commentText = GetCommentText(span.CommentType, span.StartLine.Snapshot.GetLineFromLineNumber(i));
        if (commentText.Length > 0) {
          if (sb.Length > 0)
            sb.Append(' ');
          sb.Append(commentText);
        }
      }
      return sb.ToString();
    }

    private string GetCommentText(CommentType commentType, ITextSnapshotLine line) {
      return line.GetText().Substring(GetCommentIndent(commentType, line) + commentType.Token.Length).Trim();
    }

    private int GetCommentBlockIndent(ExtendSpanResult span) {
      return Enumerable
        .Range(span.StartLine.LineNumber, span.EndLine.LineNumber - span.StartLine.LineNumber + 1)
        .Min(n => GetCommentIndent(span.CommentType, span.StartLine.Snapshot.GetLineFromLineNumber(n)));
    }

    private int GetCommentIndent(CommentType commentType, ITextSnapshotLine line) {
      return line.GetText().IndexOf(commentType.Token, StringComparison.Ordinal);
    }
  }
}
