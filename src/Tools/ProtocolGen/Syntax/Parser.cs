// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.ProtocolGen.Syntax;

/// <summary>
/// Recursive descent parser of the TypeSpec subset used by the protocol definition (see
/// protocol/README.md):
/// <code>
/// file        := (import | using | namespace | model | enum | interface)*
/// import      := "import" StringLiteral ";"
/// using       := "using" QualifiedName ";"
/// namespace   := "namespace" QualifiedName ";"
/// model       := "model" Name "{" (property | spread)* "}"
/// property    := Name "?"? ":" type (";" | ",")
/// spread      := "..." Name (";" | ",")
/// type        := Name ("[]")?
/// enum        := "enum" Name "{" (Name (":" StringLiteral)? ","?)* "}"
/// interface   := "interface" Name "{" operation* "}"
/// operation   := "op"? Name "(" (parameter ("," parameter)*)? ")" ":" (type | "void") ";"
/// parameter   := Name "?"? ":" type | "..." Name
/// </code>
/// Other TypeSpec constructs (decorators, templates, unions, aliases, scalars, inheritance, ...)
/// are reported as errors, with an explicit message.
/// </summary>
public sealed class Parser {
  private readonly string _file;
  private readonly List<Token> _tokens;
  private int _index;

  private Parser(string file, List<Token> tokens) {
    _file = file;
    _tokens = tokens;
  }

  public static SourceFile Parse(string file, string text) {
    return new Parser(file, Lexer.Tokenize(file, text)).ParseFile();
  }

  private Token Current => _tokens[_index];
  private Token PeekToken(int offset) => _tokens[Math.Min(_index + offset, _tokens.Count - 1)];

  private Token Advance() {
    var token = Current;
    if (token.Kind != TokenKind.EndOfFile)
      _index++;
    return token;
  }

  private static ProtocolDefinitionException Error(Token token, string message) => new(token.Location, message);

  private Token Expect(string punctuation) {
    if (!Current.Is(punctuation))
      throw Error(Current, $"Expected \"{punctuation}\", found {Current}");
    return Advance();
  }

  private Token ExpectIdentifier(string what) {
    if (Current.Kind != TokenKind.Identifier)
      throw Error(Current, $"Expected {what}, found {Current}");
    return Advance();
  }

  private string ParseQualifiedName() {
    var name = ExpectIdentifier("a name").Text;
    while (Current.Is(".")) {
      Advance();
      name += "." + ExpectIdentifier("a name").Text;
    }
    return name;
  }

  private SourceFile ParseFile() {
    string? ns = null;
    var imports = new List<string>();
    var declarations = new List<Declaration>();
    while (Current.Kind != TokenKind.EndOfFile) {
      var token = Current;
      if (token.Is("@") || token.Is("#"))
        throw Error(token, "Decorators and directives are not supported by the protocol generator");
      if (token.Kind != TokenKind.Identifier || token.IsEscaped)
        throw Error(token, $"Expected a declaration, found {token}");

      switch (token.Text) {
        case "import":
          Advance();
          if (Current.Kind != TokenKind.StringLiteral)
            throw Error(Current, "Expected a string literal after \"import\"");
          imports.Add(Advance().Text);
          Expect(";");
          break;
        case "using":
          Advance();
          ParseQualifiedName();
          Expect(";");
          break;
        case "namespace":
          Advance();
          var name = ParseQualifiedName();
          if (Current.Is("{"))
            throw Error(Current, "Namespace blocks are not supported: use a file level namespace (\"namespace A.B;\")");
          Expect(";");
          if (ns != null)
            throw Error(token, "Only one namespace per file is supported");
          ns = name;
          break;
        case "model":
          declarations.Add(ParseModel());
          break;
        case "enum":
          declarations.Add(ParseEnum());
          break;
        case "interface":
          declarations.Add(ParseInterface());
          break;
        case "op":
          throw Error(token, "Operations must be declared in an interface");
        case "union":
        case "alias":
        case "scalar":
        case "dec":
        case "fn":
        case "const":
        case "extern":
          throw Error(token, $"\"{token.Text}\" declarations are not supported by the protocol generator");
        default:
          throw Error(token, $"Expected a declaration, found {token}");
      }
    }
    return new SourceFile(_file, ns, imports, declarations);
  }

  private ModelDeclaration ParseModel() {
    var keyword = Advance();
    var name = ExpectIdentifier("a model name");
    if (Current.Is("<"))
      throw Error(Current, "Templates (generic models) are not supported by the protocol generator");
    if (Current.IsKeyword("extends") || Current.IsKeyword("is"))
      throw Error(Current, $"\"{Current.Text}\" is not supported (no inheritance in the protocol): use a spread (\"...Model;\") instead");
    Expect("{");
    var members = new List<ModelMember>();
    while (!Current.Is("}")) {
      if (Current.Kind == TokenKind.EndOfFile)
        throw Error(Current, $"Unterminated model \"{name.Text}\"");
      members.Add(ParseMember(isParameter: false));
      if (Current.Is(";") || Current.Is(","))
        Advance();
      else if (!Current.Is("}"))
        throw Error(Current, $"Expected \";\" after a property, found {Current}");
    }
    Expect("}");
    return new ModelDeclaration(name.Text, keyword.Doc, members, keyword.Location);
  }

  /// <summary>A property ("name?: Type") or a spread ("...Model") of a model or of operation parameters.</summary>
  private ModelMember ParseMember(bool isParameter) {
    if (Current.Is("@"))
      throw Error(Current, "Decorators are not supported by the protocol generator");
    if (Current.Is("...")) {
      var spread = Advance();
      var modelName = ExpectIdentifier("a model name");
      return new SpreadMember(modelName.Text, spread.Location);
    }
    var nameToken = ExpectIdentifier(isParameter ? "a parameter name" : "a property name");
    var optional = false;
    if (Current.Is("?")) {
      Advance();
      optional = true;
    }
    Expect(":");
    var type = ParseType();
    if (Current.Is("="))
      throw Error(Current, "Default values are not supported by the protocol generator");
    return new PropertyMember(nameToken.Text, optional, type, nameToken.Doc, nameToken.Location);
  }

  private TypeReference ParseType() {
    var token = Current;
    if (token.Kind == TokenKind.StringLiteral || token.Kind == TokenKind.Number)
      throw Error(token, "Literal types are not supported by the protocol generator: use an enum");
    if (token.Is("{"))
      throw Error(token, "Anonymous models are not supported by the protocol generator: declare a named model");
    var name = ParseQualifiedName();
    if (Current.Is("<"))
      throw Error(Current, "Template instances are not supported by the protocol generator");
    var isArray = false;
    if (Current.Is("[")) {
      Advance();
      Expect("]");
      isArray = true;
      if (Current.Is("["))
        throw Error(Current, "Arrays of arrays are not supported (protobuf compatibility): use an array of models");
    }
    if (Current.Is("|") || Current.Is("&"))
      throw Error(Current, "Union and intersection types are not supported by the protocol generator");
    return new TypeReference(name, isArray, token.Location);
  }

  private EnumDeclaration ParseEnum() {
    var keyword = Advance();
    var name = ExpectIdentifier("an enum name");
    Expect("{");
    var members = new List<EnumMemberDeclaration>();
    while (!Current.Is("}")) {
      if (Current.Is("..."))
        throw Error(Current, "Enum spreads are not supported by the protocol generator");
      var member = ExpectIdentifier("an enum member name");
      string? value = null;
      if (Current.Is(":")) {
        Advance();
        if (Current.Kind != TokenKind.StringLiteral)
          throw Error(Current, "Enum member values must be string literals");
        value = Advance().Text;
      }
      members.Add(new EnumMemberDeclaration(member.Text, value, member.Doc, member.Location));
      if (Current.Is(","))
        Advance();
      else if (!Current.Is("}"))
        throw Error(Current, $"Expected \",\" after an enum member, found {Current}");
    }
    Expect("}");
    return new EnumDeclaration(name.Text, keyword.Doc, members, keyword.Location);
  }

  private InterfaceDeclaration ParseInterface() {
    var keyword = Advance();
    var name = ExpectIdentifier("an interface name");
    if (Current.Is("<"))
      throw Error(Current, "Templates are not supported by the protocol generator");
    if (Current.IsKeyword("extends"))
      throw Error(Current, "Interface inheritance is not supported by the protocol generator");
    Expect("{");
    var operations = new List<OperationDeclaration>();
    while (!Current.Is("}")) {
      if (Current.Kind == TokenKind.EndOfFile)
        throw Error(Current, $"Unterminated interface \"{name.Text}\"");
      operations.Add(ParseOperation());
    }
    Expect("}");
    return new InterfaceDeclaration(name.Text, keyword.Doc, operations, keyword.Location);
  }

  private OperationDeclaration ParseOperation() {
    if (Current.Is("@"))
      throw Error(Current, "Decorators are not supported by the protocol generator");
    var first = Current;
    if (Current.IsKeyword("op"))
      Advance();
    var name = ExpectIdentifier("an operation name");
    if (Current.Is("<"))
      throw Error(Current, "Templates are not supported by the protocol generator");
    if (Current.IsKeyword("is"))
      throw Error(Current, "\"op ... is\" is not supported by the protocol generator");
    Expect("(");
    var parameters = new List<ModelMember>();
    while (!Current.Is(")")) {
      parameters.Add(ParseMember(isParameter: true));
      if (Current.Is(","))
        Advance();
      else if (!Current.Is(")"))
        throw Error(Current, $"Expected \",\" or \")\" in the parameters of \"{name.Text}\", found {Current}");
    }
    Expect(")");
    Expect(":");
    TypeReference? returnType = null;
    if (Current.IsKeyword("void")) {
      Advance();
    } else {
      returnType = ParseType();
    }
    Expect(";");
    return new OperationDeclaration(name.Text, first.Doc ?? name.Doc, parameters, returnType, first.Location);
  }
}
