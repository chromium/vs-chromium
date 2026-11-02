// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.ProtocolGen.Emitters;
using VsChromium.ProtocolGen.Model;
using VsChromium.ProtocolGen.Syntax;

namespace VsChromium.ProtocolGen.Tests;

[TestClass]
public class ProtocolGenTests {
  private static ProtocolDefinition Bind(params string[] files) {
    var sources = files.Select((text, i) => Parser.Parse($"file{i}.tsp", text)).ToList();
    return ProtocolBinder.Bind(sources);
  }

  private static ProtocolDefinitionException BindError(string text) {
    return Assert.ThrowsExactly<ProtocolDefinitionException>(() => Bind(text));
  }

  private const string Header = "namespace Test.Protocol;\n";

  [TestMethod]
  public void ModelsEnumsAndMethodsAreBound() {
    var protocol = Bind(Header + @"
/** A span. */
model Span { position: int32; length: int32; }

model Base { name: string; }

model Item {
  ...Base;
  /** Optional value. */
  value?: int64;
  spans: Span[];
  kind: Kind;
}

enum Kind { first, second: ""second"", `yield` }

interface Things {
  /** Gets a thing. */
  get(...Item): Item;
  remove(name: string, force?: boolean): void;
  refresh(): void;
}

interface ThingsNotifications {
  changed(...Item): void;
}
");
    Assert.AreEqual("Test.Protocol", protocol.Namespace);
    var item = protocol.Models.Single(m => m.Name == "Item");
    CollectionAssert.AreEqual(new[] { "name", "value", "spans", "kind" }, item.Fields.Select(f => f.Name).ToArray());
    Assert.AreEqual("Optional value.", item.Fields[1].Doc);
    Assert.IsTrue(item.Fields[1].IsOptional);
    Assert.AreEqual(ScalarKind.Int64, item.Fields[1].Type.Scalar);
    Assert.IsTrue(item.Fields[2].Type.IsArray);
    Assert.AreEqual("Kind", item.Fields[3].Type.Enum?.Name);
    Assert.AreEqual("A span.", protocol.Models.Single(m => m.Name == "Span").Doc);
    CollectionAssert.AreEqual(new[] { "first", "second", "yield" }, protocol.Enums.Single().Members.Select(m => m.Value).ToArray());

    CollectionAssert.AreEqual(
      new[] { "things/get", "things/remove", "things/refresh", "things/changed" },
      protocol.Methods.Select(m => m.Name).ToArray());
    var get = protocol.Methods[0];
    Assert.AreEqual(MethodKind.Request, get.Kind);
    Assert.AreEqual("Item", get.Parameters?.Name);
    Assert.AreEqual("Item", get.Result?.Name);
    Assert.AreEqual("Gets a thing.", get.Doc);
    Assert.AreEqual("ThingsGet", get.Identifier);

    // Inline parameters: synthesized model
    var remove = protocol.Methods[1];
    Assert.AreEqual("ThingsRemoveParams", remove.Parameters?.Name);
    Assert.IsTrue(remove.Parameters!.IsSynthesized);
    CollectionAssert.AreEqual(new[] { "name", "force" }, remove.Parameters.Fields.Select(f => f.Name).ToArray());
    Assert.IsNull(remove.Result);

    Assert.IsNull(protocol.Methods[2].Parameters);
    Assert.AreEqual(MethodKind.Notification, protocol.Methods[3].Kind);
  }

  [TestMethod]
  public void DocCommentsAreCleaned() {
    var protocol = Bind(Header + @"
/**
 * First line.
 *
 * Second paragraph, with ""quotes"" and <xml>.
 */
model M { a: string; }
");
    Assert.AreEqual("First line.\n\nSecond paragraph, with \"quotes\" and <xml>.", protocol.Models[0].Doc);
  }

  [TestMethod]
  [DataRow("@doc(\"x\") model M { a: string; }", "Decorators")]
  [DataRow("model M { @doc(\"x\") a: string; }", "Decorators")]
  [DataRow("model M<T> { a: T; }", "Templates")]
  [DataRow("model A { a: string; } model M extends A { b: string; }", "inheritance")]
  [DataRow("model M is Record<string> { }", "inheritance")]
  [DataRow("model M { a: string | int32; }", "Union")]
  [DataRow("model M { a: \"x\"; }", "Literal types")]
  [DataRow("model M { a: { b: string }; }", "Anonymous models")]
  [DataRow("model M { a: string = \"x\"; }", "Default values")]
  [DataRow("model M { a: string[][]; }", "Arrays of arrays")]
  [DataRow("model M { a?: string[]; }", "Optional arrays")]
  [DataRow("model M { a: Unknown; }", "Unknown type")]
  [DataRow("model M { a: uint32; }", "not supported")]
  [DataRow("model M { a: string; a: int32; }", "Duplicate property")]
  [DataRow("model M { a: string; } model M { b: string; }", "Duplicate declaration")]
  [DataRow("model m { a: string; }", "PascalCase")]
  [DataRow("model M { A: string; }", "camelCase")]
  [DataRow("model M { ...M; }", "spreads itself")]
  [DataRow("union U { a: string }", "not supported")]
  [DataRow("alias A = string;", "not supported")]
  [DataRow("op get(): void;", "must be declared in an interface")]
  [DataRow("interface I { get(): string; }", "must be a model")]
  [DataRow("model M { a: string; } interface I { get(): M[]; }", "must be a model")]
  [DataRow("model M { a: string; } interface INotifications { changed(): M; }", "must return void")]
  [DataRow("interface I { get(): void; } interface I2 { x(): void; } interface INotifications { get(): void; }", "Duplicate method")]
  [DataRow("enum E { }", "no member")]
  [DataRow("enum E { a: \"b\", b }", "Duplicate enum value")]
  [DataRow("namespace Other { }", "Namespace blocks")]
  [DataRow("model M { a: string }\nmodel N { b: int32 }\n#suppress \"x\"", "directives")]
  public void UnsupportedConstructsAreErrors(string text, string expectedMessage) {
    var error = BindError(Header + text);
    StringAssert.Contains(error.Message, expectedMessage);
    StringAssert.StartsWith(error.Message, "file0.tsp(");
  }

  [TestMethod]
  public void ErrorsHaveLocations() {
    var error = BindError(Header + "model M {\n  a: string;\n  b: Missing;\n}\n");
    Assert.AreEqual(4, error.Location.Line);
    Assert.AreEqual(6, error.Location.Column);
  }

  [TestMethod]
  public void AllFilesMustDeclareTheSameNamespace() {
    var error = Assert.ThrowsExactly<ProtocolDefinitionException>(() =>
      Bind(Header + "model A { a: string; }", "namespace Other;\nmodel B { b: string; }"));
    StringAssert.Contains(error.Message, "same namespace");
  }

  [TestMethod]
  public void ImportsMustReferToDefinitionFiles() {
    var error = Assert.ThrowsExactly<ProtocolDefinitionException>(() =>
      Bind("import \"./missing.tsp\";\n" + Header + "model A { a: string; }"));
    StringAssert.Contains(error.Message, "missing.tsp");
    Bind("import \"./file1.tsp\";\n", Header + "model A { a: string; }");
  }

  [TestMethod]
  public void ServerCodeIsGenerated() {
    var protocol = Bind(Header + @"
enum Status { idle, busy }
model Span { position: int32; length: int32; }
model Result { spans: Span[]; status: Status; label?: string; when: utcDateTime; count?: int64; }
interface Search { code(text: string): Result; refresh(): void; }
interface SearchNotifications { done(...Result): void; }
");
    var code = CSharpServerEmitter.Emit(protocol);
    StringAssert.Contains(code, "namespace Test.Protocol.Generated {");
    StringAssert.Contains(code, "[JsonStringEnumMemberName(\"idle\")]\n    Idle,");
    StringAssert.Contains(code, "[JsonPropertyName(\"spans\")]\n    public List<Span> Spans { get; set; } = new();");
    StringAssert.Contains(code, "[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]\n    public string? Label { get; set; }");
    StringAssert.Contains(code, "public DateTime When { get; set; }");
    StringAssert.Contains(code, "public long? Count { get; set; }");
    StringAssert.Contains(code, "public sealed partial class SearchCodeParams {");
    StringAssert.Contains(code, "public const string SearchCode = \"search/code\";");
    StringAssert.Contains(code, "ValueTask<Result> SearchCodeAsync(SearchCodeParams parameters, CancellationToken cancellationToken);");
    StringAssert.Contains(code, "ValueTask SearchRefreshAsync(CancellationToken cancellationToken);");
    StringAssert.Contains(code, "public void SearchDone(Result parameters) =>");
    StringAssert.Contains(code, "[JsonSerializable(typeof(SearchCodeParams))]");
  }

  /// <summary>
  /// The generated files of the repository must be up to date with the protocol definition (they
  /// are regenerated by the build: this test catches a stale checked-in file).
  /// </summary>
  [TestMethod]
  public void RepositoryGeneratedFilesAreUpToDate() {
    var root = RepositoryRoot();
    var protocol = ProtocolBinder.LoadDirectory(Path.Combine(root, "protocol"));
    var expected = CSharpServerEmitter.Emit(protocol);
    var actual = File.ReadAllText(Path.Combine(root, "src", "Core.Server", "Protocol", "Generated", "Protocol.g.cs")).Replace("\r\n", "\n");
    Assert.AreEqual(expected, actual, "src/Core.Server/Protocol/Generated/Protocol.g.cs is not up to date: build src/Core.Server");
  }

  private static string RepositoryRoot() {
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent) {
      if (File.Exists(Path.Combine(dir.FullName, "build.root")))
        return dir.FullName;
    }
    throw new InvalidOperationException("Repository root (build.root) not found");
  }
}
