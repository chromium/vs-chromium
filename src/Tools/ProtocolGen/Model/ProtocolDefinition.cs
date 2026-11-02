// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.ProtocolGen.Syntax;

namespace VsChromium.ProtocolGen.Model;

public enum ScalarKind {
  String,
  Boolean,
  Int32,
  Int64,
  Float64,
  UtcDateTime,
}

/// <summary>The type of a field: a scalar, a model or an enum, optionally an array.</summary>
public sealed record FieldType(ScalarKind? Scalar, ProtocolModel? Model, ProtocolEnum? Enum, bool IsArray) {
  public string Name => Scalar?.ToString() ?? Model?.Name ?? Enum!.Name;
}

public sealed record ProtocolField(string Name, string? Doc, bool IsOptional, FieldType Type);

public sealed class ProtocolModel(string name, string? doc, SourceLocation location) {
  public string Name { get; } = name;
  public string? Doc { get; } = doc;
  public SourceLocation Location { get; } = location;
  /// <summary>The fields, spreads expanded (in declaration order).</summary>
  public List<ProtocolField> Fields { get; } = new();
  /// <summary>True for the parameter models synthesized for operations with inline parameters.</summary>
  public bool IsSynthesized { get; init; }
}

public sealed record ProtocolEnumMember(string Name, string Value, string? Doc);

public sealed record ProtocolEnum(string Name, string? Doc, IReadOnlyList<ProtocolEnumMember> Members);

public enum MethodKind {
  /// <summary>Client to server request, with a result (possibly "void").</summary>
  Request,
  /// <summary>Server to client notification (no result).</summary>
  Notification,
}

/// <summary>A JSON-RPC method: "&lt;group&gt;/&lt;operation&gt;", e.g. "search/code".</summary>
public sealed record ProtocolMethod(
  string Name,
  MethodKind Kind,
  string InterfaceName,
  string OperationName,
  string? Doc,
  ProtocolModel? Parameters,
  ProtocolModel? Result) {
  /// <summary>PascalCase identifier of the method, e.g. "SearchCode".</summary>
  public string Identifier => string.Concat(Name.Split('/').Select(Naming.Pascal));
}

/// <summary>The resolved protocol: all the models, enums and methods of the definition files.</summary>
public sealed class ProtocolDefinition {
  public required string Namespace { get; init; }
  public required IReadOnlyList<ProtocolModel> Models { get; init; }
  public required IReadOnlyList<ProtocolEnum> Enums { get; init; }
  public required IReadOnlyList<ProtocolMethod> Methods { get; init; }

  public IEnumerable<ProtocolMethod> Requests => Methods.Where(m => m.Kind == MethodKind.Request);
  public IEnumerable<ProtocolMethod> Notifications => Methods.Where(m => m.Kind == MethodKind.Notification);
}

public static class Naming {
  public static string Pascal(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
  public static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
}
