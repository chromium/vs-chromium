using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;

namespace VsChromiumPackage.Features.FormatComment {
  public class CommentFormatter {
    private const string _defaultLineEnding = "\n";
    private const int _maxColumn = 80;
    private const string _commentToken = "//";
    private const string _commentPrefixText= _commentToken + " ";

    public Tuple<ITextSnapshotLine, ITextSnapshotLine> ExtendSpan(SnapshotSpan span) {
      // If no selection, extend lines up and down.
      var deltaLine = (span.Length == 0 ? 1 : 0);

      var startLine = FindCommentLine(span, span.Start.Position, -deltaLine);
      if (startLine == null)
        return null;

      var endLine = FindCommentLine(span, span.End.Position, deltaLine);
      if (endLine == null)
        return null;

      // Check all lines are comments lines
      for (var i = startLine.LineNumber; i <= endLine.LineNumber; i++) {
        if (!IsCommentLine(span.Snapshot.GetLineFromLineNumber(i)))
          return null;
      }

      // Adjust end line in if 1) selection and 2) cursor at start of next line
      if (endLine.LineNumber > startLine.LineNumber &&
          deltaLine == 0 &&
          span.End == endLine.Start) {
        endLine = span.Snapshot.GetLineFromLineNumber(endLine.LineNumber - 1);
      }
      return Tuple.Create(startLine, endLine);
    }

    public class FormatLinesResult {
      public SnapshotSpan SnapshotSpan { get; set; }
      public int Indent { get; set; }
      public IList<string> Lines { get; set; }
    }

    public FormatLinesResult FormatLines(ITextSnapshotLine start, ITextSnapshotLine end) {
      var indent = GetCommentBlockIndent(start, end);
      if (indent < 0)
        throw new ArgumentException("Line range does not contains a block comment.");

      var commentText = GetCommentBlockText(start, end);

      return new FormatLinesResult {
        SnapshotSpan = new SnapshotSpan(start.Snapshot, new Span(start.Start, end.End - start.Start)),
        Indent = indent,
        Lines = FormatCommentText(commentText, _maxColumn - _commentPrefixText.Length - indent).ToList()
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
        sb.Append(_commentPrefixText);
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

    private bool IsCommentLine(ITextSnapshotLine getLineFromLineNumber) {
      var text = getLineFromLineNumber.GetText().Trim();
      return text.StartsWith(_commentToken);
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

    struct LineData {
      private readonly int _startIndex;
      private readonly int _length;
      private readonly int _newIndex;

      public LineData(int startIndex, int length, int newIndex) {
        this._startIndex = startIndex;
        this._length = length;
        this._newIndex = newIndex;
      }

      public int StartIndex {
        get {
          return this._startIndex;
        }
      }

      public int Length {
        get {
          return this._length;
        }
      }

      public int NewIndex {
        get {
          return this._newIndex;
        }
      }
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

    private string GetCommentBlockText(ITextSnapshotLine start, ITextSnapshotLine end) {
      var sb = new StringBuilder();
      for (var i = start.LineNumber; i <= end.LineNumber; i++) {
        var commentText = GetCommentText(start.Snapshot.GetLineFromLineNumber(i));
        if (commentText.Length > 0) {
          if (sb.Length > 0)
            sb.Append(' ');
          sb.Append(commentText);
        }
      }
      return sb.ToString();
    }

    private string GetCommentText(ITextSnapshotLine line) {
      return line.GetText().Substring(GetCommentIndent(line) + 2).Trim();
    }

    private int GetCommentBlockIndent(ITextSnapshotLine start, ITextSnapshotLine end) {
      return Enumerable
          .Range(start.LineNumber, end.LineNumber - start.LineNumber + 1)
          .Min(n => GetCommentIndent(start.Snapshot.GetLineFromLineNumber(n)));
    }

    private int GetCommentIndent(ITextSnapshotLine line) {
      return line.GetText().IndexOf(_commentToken);
    }
  }
}
