// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.ProtocolGen.Syntax;

/// <summary>Syntax tree of a protocol definition file (TypeSpec subset).</summary>
public sealed record SourceFile(string Path, string? Namespace, IReadOnlyList<string> Imports, IReadOnlyList<Declaration> Declarations);

public abstract record Declaration(string Name, string? Doc, SourceLocation Location);

/// <summary>A reference to a type: a scalar or declared type name, optionally an array ("T[]").</summary>
public sealed record TypeReference(string Name, bool IsArray, SourceLocation Location) {
  public override string ToString() => IsArray ? Name + "[]" : Name;
}

public abstract record ModelMember(SourceLocation Location);

/// <summary>"name?: Type;"</summary>
public sealed record PropertyMember(string Name, bool IsOptional, TypeReference Type, string? Doc, SourceLocation Location) : ModelMember(Location);

/// <summary>"...Model;": the properties of another model.</summary>
public sealed record SpreadMember(string ModelName, SourceLocation Location) : ModelMember(Location);

public sealed record ModelDeclaration(string Name, string? Doc, IReadOnlyList<ModelMember> Members, SourceLocation Location)
  : Declaration(Name, Doc, Location);

public sealed record EnumMemberDeclaration(string Name, string? Value, string? Doc, SourceLocation Location);

public sealed record EnumDeclaration(string Name, string? Doc, IReadOnlyList<EnumMemberDeclaration> Members, SourceLocation Location)
  : Declaration(Name, Doc, Location);

/// <summary>"name(params): ReturnType;" (ReturnType null for "void").</summary>
public sealed record OperationDeclaration(string Name, string? Doc, IReadOnlyList<ModelMember> Parameters, TypeReference? ReturnType, SourceLocation Location);

public sealed record InterfaceDeclaration(string Name, string? Doc, IReadOnlyList<OperationDeclaration> Operations, SourceLocation Location)
  : Declaration(Name, Doc, Location);
