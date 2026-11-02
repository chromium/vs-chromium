// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;

namespace VsChromium.ProtocolGen.Syntax;

public enum TokenKind {
  Identifier,
  StringLiteral,
  Number,
  Punctuation,
  EndOfFile,
}

/// <summary>A location in a source file (1 based line and column), for error messages.</summary>
public readonly record struct SourceLocation(string File, int Line, int Column) {
  public override string ToString() => $"{File}({Line},{Column})";
}

public sealed record Token(TokenKind Kind, string Text, SourceLocation Location, string? Doc) {
  public bool Is(string punctuation) => Kind == TokenKind.Punctuation && Text == punctuation;
  public bool IsKeyword(string keyword) => Kind == TokenKind.Identifier && !IsEscaped && Text == keyword;

  /// <summary>True for identifiers escaped with backticks (e.g. `yield`): never keywords.</summary>
  public bool IsEscaped { get; init; }

  public override string ToString() => Kind == TokenKind.EndOfFile ? "end of file" : $"\"{Text}\"";
}

/// <summary>An error in a protocol definition file (syntax or semantic error).</summary>
public sealed class ProtocolDefinitionException(SourceLocation location, string message)
  : Exception($"{location}: error: {message}") {
  public SourceLocation Location { get; } = location;
}

/// <summary>
/// Lexer of the TypeSpec subset: identifiers (optionally escaped with backticks), string literals,
/// numbers, punctuation. Comments are skipped; documentation comments ("/** ... */") are attached to
/// the next token (see <see cref="Token.Doc"/>).
/// </summary>
public sealed class Lexer {
  private static readonly string[] Punctuations = { "...", "{", "}", "(", ")", "[", "]", "<", ">", ";", ":", ",", ".", "?", "|", "&", "@", "=", "#" };

  private readonly string _file;
  private readonly string _text;
  private int _position;
  private int _line = 1;
  private int _column = 1;

  public Lexer(string file, string text) {
    _file = file;
    _text = text;
  }

  public static List<Token> Tokenize(string file, string text) {
    var lexer = new Lexer(file, text);
    var result = new List<Token>();
    while (true) {
      var token = lexer.Next();
      result.Add(token);
      if (token.Kind == TokenKind.EndOfFile)
        return result;
    }
  }

  private char Current => _position < _text.Length ? _text[_position] : '\0';
  private char Peek(int offset) => _position + offset < _text.Length ? _text[_position + offset] : '\0';
  private SourceLocation Location => new(_file, _line, _column);

  private void Advance(int count = 1) {
    for (var i = 0; i < count && _position < _text.Length; i++) {
      if (_text[_position] == '\n') {
        _line++;
        _column = 1;
      } else {
        _column++;
      }
      _position++;
    }
  }

  public Token Next() {
    string? doc = null;
    while (true) {
      // Whitespace (and a BOM)
      while (_position < _text.Length && (char.IsWhiteSpace(Current) || Current == '\uFEFF'))
        Advance();

      if (Current == '/' && Peek(1) == '/') {
        while (_position < _text.Length && Current != '\n')
          Advance();
        continue;
      }

      if (Current == '/' && Peek(1) == '*') {
        var start = Location;
        var isDoc = Peek(2) == '*' && Peek(3) != '/';
        var end = _text.IndexOf("*/", _position + 2, StringComparison.Ordinal);
        if (end < 0)
          throw new ProtocolDefinitionException(start, "Unterminated comment");
        var body = _text.Substring(_position + (isDoc ? 3 : 2), end - _position - (isDoc ? 3 : 2));
        Advance(end + 2 - _position);
        if (isDoc)
          doc = CleanDocComment(body);
        continue;
      }
      break;
    }

    var location = Location;
    if (_position >= _text.Length)
      return new Token(TokenKind.EndOfFile, "", location, doc);

    var c = Current;
    if (char.IsLetter(c) || c == '_' || c == '$') {
      var start = _position;
      while (char.IsLetterOrDigit(Current) || Current == '_' || Current == '$')
        Advance();
      return new Token(TokenKind.Identifier, _text.Substring(start, _position - start), location, doc);
    }

    if (c == '`') {
      Advance();
      var start = _position;
      while (_position < _text.Length && Current != '`' && Current != '\n')
        Advance();
      if (Current != '`')
        throw new ProtocolDefinitionException(location, "Unterminated escaped identifier");
      var name = _text.Substring(start, _position - start);
      Advance();
      return new Token(TokenKind.Identifier, name, location, doc) { IsEscaped = true };
    }

    if (char.IsDigit(c)) {
      var start = _position;
      while (char.IsLetterOrDigit(Current) || Current == '.')
        Advance();
      return new Token(TokenKind.Number, _text.Substring(start, _position - start), location, doc);
    }

    if (c == '"') {
      if (Peek(1) == '"' && Peek(2) == '"')
        throw new ProtocolDefinitionException(location, "Triple-quoted strings are not supported");
      Advance();
      var sb = new StringBuilder();
      while (_position < _text.Length && Current != '"' && Current != '\n') {
        if (Current == '\\') {
          Advance();
          sb.Append(Current switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => Current });
        } else if (Current == '$' && Peek(1) == '{') {
          throw new ProtocolDefinitionException(Location, "String templates are not supported");
        } else {
          sb.Append(Current);
        }
        Advance();
      }
      if (Current != '"')
        throw new ProtocolDefinitionException(location, "Unterminated string literal");
      Advance();
      return new Token(TokenKind.StringLiteral, sb.ToString(), location, doc);
    }

    foreach (var punctuation in Punctuations) {
      if (string.CompareOrdinal(_text, _position, punctuation, 0, punctuation.Length) == 0) {
        Advance(punctuation.Length);
        return new Token(TokenKind.Punctuation, punctuation, location, doc);
      }
    }

    throw new ProtocolDefinitionException(location, $"Unexpected character '{c}'");
  }

  /// <summary>
  /// The text of a documentation comment: leading "*" of each line removed, lines trimmed, empty
  /// first/last lines removed. Paragraphs (empty lines) are kept.
  /// </summary>
  private static string CleanDocComment(string body) {
    var lines = body.Replace("\r\n", "\n").Split('\n')
      .Select(line => {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('*'))
          trimmed = trimmed.Substring(1).Trim();
        return trimmed;
      })
      .ToList();
    while (lines.Count > 0 && lines[0].Length == 0)
      lines.RemoveAt(0);
    while (lines.Count > 0 && lines[^1].Length == 0)
      lines.RemoveAt(lines.Count - 1);
    return string.Join("\n", lines);
  }
}
