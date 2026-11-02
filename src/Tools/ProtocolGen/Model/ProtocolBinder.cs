// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.RegularExpressions;
using VsChromium.ProtocolGen.Syntax;

namespace VsChromium.ProtocolGen.Model;

/// <summary>
/// Builds the <see cref="ProtocolDefinition"/> from the syntax trees of the definition files:
/// resolves type names, expands spreads, synthesizes the parameter models of operations with inline
/// parameters, and checks the rules of the protocol (see protocol/README.md):
/// <list type="bullet">
/// <item>Names: models, enums and interfaces in PascalCase; properties, parameters, operations and
/// enum members in camelCase (they are the JSON names).</item>
/// <item>Methods: "&lt;group&gt;/&lt;operation&gt;", where the group is the camelCase interface
/// name. Interfaces whose name ends with "Notifications" declare server to client notifications
/// (operations returning void).</item>
/// <item>Protobuf compatibility: results are models; no optional arrays; no arrays of arrays.</item>
/// </list>
/// </summary>
public sealed class ProtocolBinder {
  public const string NotificationsSuffix = "Notifications";

  private static readonly Dictionary<string, ScalarKind> Scalars = new() {
    ["string"] = ScalarKind.String,
    ["boolean"] = ScalarKind.Boolean,
    ["int32"] = ScalarKind.Int32,
    ["int64"] = ScalarKind.Int64,
    ["float64"] = ScalarKind.Float64,
    ["utcDateTime"] = ScalarKind.UtcDateTime,
  };

  private static readonly HashSet<string> UnsupportedScalars = new() {
    "int8", "int16", "uint8", "uint16", "uint32", "uint64", "safeint", "integer", "numeric", "float",
    "float32", "decimal", "decimal128", "bytes", "plainDate", "plainTime", "offsetDateTime", "duration",
    "url", "unknown", "null", "never", "Record", "Array",
  };

  private static readonly Regex PascalName = new("^[A-Z][A-Za-z0-9]*$");
  private static readonly Regex CamelName = new("^[a-z][A-Za-z0-9]*$");

  private readonly Dictionary<string, ModelDeclaration> _modelDeclarations = new();
  private readonly Dictionary<string, ProtocolModel> _models = new();
  private readonly Dictionary<string, ProtocolEnum> _enums = new();
  private readonly HashSet<string> _resolvedModels = new();
  private readonly HashSet<string> _resolvingModels = new();
  private readonly List<ProtocolModel> _orderedModels = new();

  public static ProtocolDefinition LoadDirectory(string directory) {
    var files = Directory.GetFiles(directory, "*.tsp").OrderBy(x => x, StringComparer.Ordinal).ToList();
    if (files.Count == 0)
      throw new InvalidOperationException($"No .tsp file found in \"{directory}\"");
    var sources = files.Select(f => Parser.Parse(Path.GetFileName(f), File.ReadAllText(f))).ToList();
    return Bind(sources);
  }

  public static ProtocolDefinition Bind(IReadOnlyList<SourceFile> files) => new ProtocolBinder().BindFiles(files);

  private ProtocolDefinition BindFiles(IReadOnlyList<SourceFile> files) {
    // Imports must refer to files of the definition (they are not needed by the generator, which
    // loads all the files of the directory, but by the TypeSpec tools).
    var fileNames = new HashSet<string>(files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
    foreach (var file in files) {
      foreach (var import in file.Imports) {
        var name = import.StartsWith("./", StringComparison.Ordinal) ? import.Substring(2) : import;
        if (!fileNames.Contains(name))
          throw new ProtocolDefinitionException(new SourceLocation(file.Path, 1, 1), $"Import \"{import}\" doesn't refer to a definition file of the directory");
      }
    }

    // A single namespace, declared by all the files with declarations.
    var namespaces = files.Where(f => f.Declarations.Count > 0).Select(f => f.Namespace).Distinct().ToList();
    var withoutNamespace = files.FirstOrDefault(f => f.Declarations.Count > 0 && f.Namespace == null);
    if (withoutNamespace != null)
      throw new ProtocolDefinitionException(withoutNamespace.Declarations[0].Location, "Missing namespace declaration (\"namespace A.B;\")");
    if (namespaces.Count != 1)
      throw new ProtocolDefinitionException(new SourceLocation(files[0].Path, 1, 1), $"All the files must declare the same namespace (found: {string.Join(", ", namespaces)})");

    // Declarations by name
    var declarations = new Dictionary<string, Declaration>();
    var orderedDeclarations = files.SelectMany(f => f.Declarations).ToList();
    foreach (var declaration in orderedDeclarations) {
      CheckName(declaration.Name, PascalName, "PascalCase", declaration.Location);
      if (Scalars.ContainsKey(declaration.Name) || UnsupportedScalars.Contains(declaration.Name))
        throw new ProtocolDefinitionException(declaration.Location, $"\"{declaration.Name}\" is a built-in type name");
      if (!declarations.TryAdd(declaration.Name, declaration))
        throw new ProtocolDefinitionException(declaration.Location, $"Duplicate declaration \"{declaration.Name}\" (first declared at {declarations[declaration.Name].Location})");
    }

    foreach (var declaration in orderedDeclarations.OfType<EnumDeclaration>()) {
      _enums.Add(declaration.Name, BindEnum(declaration));
    }
    foreach (var declaration in orderedDeclarations.OfType<ModelDeclaration>()) {
      _modelDeclarations.Add(declaration.Name, declaration);
      _models.Add(declaration.Name, new ProtocolModel(declaration.Name, declaration.Doc, declaration.Location));
    }
    foreach (var declaration in orderedDeclarations.OfType<ModelDeclaration>()) {
      ResolveModel(declaration);
    }

    var methods = new List<ProtocolMethod>();
    var methodNames = new Dictionary<string, SourceLocation>();
    foreach (var declaration in orderedDeclarations.OfType<InterfaceDeclaration>()) {
      foreach (var method in BindInterface(declaration)) {
        if (!methodNames.TryAdd(method.Name, declaration.Location))
          throw new ProtocolDefinitionException(declaration.Location, $"Duplicate method \"{method.Name}\"");
        methods.Add(method);
      }
    }

    return new ProtocolDefinition {
      Namespace = namespaces[0]!,
      Models = _orderedModels,
      Enums = _enums.Values.ToList(),
      Methods = methods,
    };
  }

  private static void CheckName(string name, Regex regex, string style, SourceLocation location) {
    if (!regex.IsMatch(name))
      throw new ProtocolDefinitionException(location, $"\"{name}\" must be a {style} name (letters and digits)");
  }

  private static ProtocolEnum BindEnum(EnumDeclaration declaration) {
    var members = new List<ProtocolEnumMember>();
    var values = new HashSet<string>();
    foreach (var member in declaration.Members) {
      CheckName(member.Name, CamelName, "camelCase", member.Location);
      var value = member.Value ?? member.Name;
      CheckName(value, CamelName, "camelCase", member.Location);
      if (!values.Add(value))
        throw new ProtocolDefinitionException(member.Location, $"Duplicate enum value \"{value}\"");
      members.Add(new ProtocolEnumMember(member.Name, value, member.Doc));
    }
    if (members.Count == 0)
      throw new ProtocolDefinitionException(declaration.Location, $"Enum \"{declaration.Name}\" has no member");
    return new ProtocolEnum(declaration.Name, declaration.Doc, members);
  }

  /// <summary>Resolves the fields of a model (after the models it spreads).</summary>
  private void ResolveModel(ModelDeclaration declaration) {
    if (_resolvedModels.Contains(declaration.Name))
      return;
    if (!_resolvingModels.Add(declaration.Name))
      throw new ProtocolDefinitionException(declaration.Location, $"Model \"{declaration.Name}\" spreads itself (directly or indirectly)");
    var model = _models[declaration.Name];
    AddMembers(model, declaration.Members);
    _resolvingModels.Remove(declaration.Name);
    _resolvedModels.Add(declaration.Name);
    _orderedModels.Add(model);
  }

  private void AddMembers(ProtocolModel model, IEnumerable<ModelMember> members) {
    foreach (var member in members) {
      switch (member) {
        case SpreadMember spread:
          if (!_modelDeclarations.TryGetValue(spread.ModelName, out var spreadDeclaration))
            throw new ProtocolDefinitionException(spread.Location, $"Unknown model \"{spread.ModelName}\" (only models can be spread)");
          ResolveModel(spreadDeclaration);
          foreach (var field in _models[spread.ModelName].Fields) {
            AddField(model, field, spread.Location);
          }
          break;
        case PropertyMember property:
          CheckName(property.Name, CamelName, "camelCase", property.Location);
          var type = ResolveType(property.Type);
          if (property.IsOptional && type.IsArray)
            throw new ProtocolDefinitionException(property.Location, $"Optional arrays are not supported (protobuf compatibility): make \"{property.Name}\" required (empty array when there are no values)");
          AddField(model, new ProtocolField(property.Name, property.Doc, property.IsOptional, type), property.Location);
          break;
      }
    }
  }

  private static void AddField(ProtocolModel model, ProtocolField field, SourceLocation location) {
    if (model.Fields.Any(f => f.Name == field.Name))
      throw new ProtocolDefinitionException(location, $"Duplicate property \"{field.Name}\" in \"{model.Name}\"");
    model.Fields.Add(field);
  }

  private FieldType ResolveType(TypeReference reference) {
    if (Scalars.TryGetValue(reference.Name, out var scalar))
      return new FieldType(scalar, null, null, reference.IsArray);
    if (_models.TryGetValue(reference.Name, out var model))
      return new FieldType(null, model, null, reference.IsArray);
    if (_enums.TryGetValue(reference.Name, out var protocolEnum))
      return new FieldType(null, null, protocolEnum, reference.IsArray);
    if (reference.Name == "void")
      throw new ProtocolDefinitionException(reference.Location, "\"void\" is only allowed as an operation result");
    if (UnsupportedScalars.Contains(reference.Name))
      throw new ProtocolDefinitionException(reference.Location, $"Type \"{reference.Name}\" is not supported by the protocol generator (supported scalars: {string.Join(", ", Scalars.Keys)})");
    throw new ProtocolDefinitionException(reference.Location, $"Unknown type \"{reference.Name}\"");
  }

  private IEnumerable<ProtocolMethod> BindInterface(InterfaceDeclaration declaration) {
    var isNotifications = declaration.Name.EndsWith(NotificationsSuffix, StringComparison.Ordinal) &&
                          declaration.Name.Length > NotificationsSuffix.Length;
    var group = Naming.Camel(isNotifications ? declaration.Name.Substring(0, declaration.Name.Length - NotificationsSuffix.Length) : declaration.Name);
    foreach (var operation in declaration.Operations) {
      CheckName(operation.Name, CamelName, "camelCase", operation.Location);
      var methodName = group + "/" + operation.Name;

      ProtocolModel? result = null;
      if (operation.ReturnType != null) {
        if (isNotifications)
          throw new ProtocolDefinitionException(operation.Location, $"Notification \"{methodName}\" must return void");
        var type = ResolveType(operation.ReturnType);
        if (type.Model == null || type.IsArray)
          throw new ProtocolDefinitionException(operation.ReturnType.Location, $"The result of \"{methodName}\" must be a model (protobuf compatibility), found \"{operation.ReturnType}\"");
        result = type.Model;
      }

      yield return new ProtocolMethod(
        methodName,
        isNotifications ? MethodKind.Notification : MethodKind.Request,
        declaration.Name,
        operation.Name,
        operation.Doc,
        BindParameters(operation, group),
        result);
    }
  }

  /// <summary>
  /// The parameters model of an operation: none, the spread model ("op(...Model)"), or a model
  /// synthesized from the inline parameters ("&lt;Group&gt;&lt;Operation&gt;Params").
  /// </summary>
  private ProtocolModel? BindParameters(OperationDeclaration operation, string group) {
    if (operation.Parameters.Count == 0)
      return null;
    if (operation.Parameters is [SpreadMember spread]) {
      if (!_models.TryGetValue(spread.ModelName, out var spreadModel))
        throw new ProtocolDefinitionException(spread.Location, $"Unknown model \"{spread.ModelName}\" (only models can be spread)");
      return spreadModel;
    }
    var name = Naming.Pascal(group) + Naming.Pascal(operation.Name) + "Params";
    if (_models.ContainsKey(name) || _enums.ContainsKey(name))
      throw new ProtocolDefinitionException(operation.Location, $"The parameters model \"{name}\" of operation \"{operation.Name}\" conflicts with a declaration of the same name");
    var model = new ProtocolModel(name, null, operation.Location) { IsSynthesized = true };
    AddMembers(model, operation.Parameters);
    _models.Add(name, model);
    _orderedModels.Add(model);
    return model;
  }
}
