// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;

namespace VsChromium.ProtocolGen.Emitters;

/// <summary>Builds indented source code (LF line endings, 2 spaces per level).</summary>
public sealed class CodeWriter {
  private readonly StringBuilder _sb = new();
  private int _indent;

  public CodeWriter Line(string text = "") {
    if (text.Length > 0)
      _sb.Append(' ', _indent * 2).Append(text);
    _sb.Append('\n');
    return this;
  }

  public CodeWriter Open(string text) {
    Line(text + " {");
    _indent++;
    return this;
  }

  public CodeWriter Close(string suffix = "") {
    _indent--;
    Line("}" + suffix);
    return this;
  }

  /// <summary>
  /// A C# XML documentation comment (summary), if <paramref name="doc"/> is not null. Documentation of
  /// the definition files is escaped; documentation written by the generator (<paramref
  /// name="escape"/> false) may contain XML tags.
  /// </summary>
  public CodeWriter CSharpDoc(string? doc, bool escape = true) {
    if (string.IsNullOrEmpty(doc))
      return this;
    Line("/// <summary>");
    foreach (var line in doc.Split('\n')) {
      Line(line.Length == 0 ? "///" : "/// " + (escape ? EscapeXml(line) : line));
    }
    Line("/// </summary>");
    return this;
  }

  /// <summary>A JSDoc comment, if <paramref name="doc"/> is not null.</summary>
  public CodeWriter JsDoc(string? doc) {
    if (string.IsNullOrEmpty(doc))
      return this;
    var lines = doc.Split('\n');
    if (lines.Length == 1) {
      Line("/** " + lines[0].Replace("*/", "*\\/") + " */");
      return this;
    }
    Line("/**");
    foreach (var line in lines) {
      Line(line.Length == 0 ? " *" : " * " + line.Replace("*/", "*\\/"));
    }
    Line(" */");
    return this;
  }

  private static string EscapeXml(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

  public override string ToString() => _sb.ToString();
}
