// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace VsChromium.Core.Ipc.Json {
  /// <summary>
  /// Serializes <see cref="IpcMessage"/> instances to/from JSON. This is the protocol between the
  /// clients (Visual Studio package, VS Code extension) and the server, over the server standard
  /// input/output (see <see cref="IpcStreamOverJsonLines"/>).
  ///
  /// Uses System.Text.Json with source generated metadata (<see cref="IpcJsonContext"/>): no
  /// reflection at run time.
  ///
  /// The JSON representation:
  /// <list type="bullet">
  /// <item>The public read/write properties of the message classes are serialized, as well as
  /// read-only collection properties (populated when deserializing). Other read-only properties
  /// (computed values, e.g. <c>DirectoryEntry.IsEmpty</c>) are not.</item>
  /// <item>Property names are camelCase (matched case-insensitively when reading). Null values are
  /// omitted.</item>
  /// <item>Polymorphic values (i.e. values of a sub-class of the declared type of a property) carry a
  /// <c>"$type"</c> property containing the short class name, e.g. <c>"SearchCodeRequest"</c>, which
  /// may appear anywhere in the object when reading. Only the message classes (<see
  /// cref="MessageTypes"/>) are accepted.</item>
  /// <item>Enums are serialized as strings, <see cref="DateTime"/> values as ISO 8601 UTC.</item>
  /// </list>
  /// </summary>
  public static class IpcJsonSerializer {
    private const string TypeDiscriminatorPropertyName = "$type";

    /// <summary>
    /// For each message base class, the message classes deriving from it (directly or not).
    /// Note: must be initialized before <see cref="Options"/> is used.
    /// </summary>
    private static readonly Dictionary<Type, Type[]> DerivedTypes = MessageTypes.All
      .Select(baseType => new {
        BaseType = baseType,
        Derived = MessageTypes.All
          .Where(t => t != baseType && !t.IsAbstract && baseType.IsAssignableFrom(t))
          .OrderBy(t => t.Name, StringComparer.Ordinal)
          .ToArray()
      })
      .Where(x => x.Derived.Length > 0)
      .ToDictionary(x => x.BaseType, x => x.Derived);

    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static readonly JsonTypeInfo<IpcMessage> MessageTypeInfo =
      (JsonTypeInfo<IpcMessage>)Options.GetTypeInfo(typeof(IpcMessage));

    public static string Serialize(IpcMessage message) {
      return JsonSerializer.Serialize(message, MessageTypeInfo);
    }

    /// <summary>
    /// Serializes <paramref name="message"/> as UTF-8 JSON to <paramref name="writer"/>.
    /// </summary>
    public static void Serialize(Utf8JsonWriter writer, IpcMessage message) {
      JsonSerializer.Serialize(writer, message, MessageTypeInfo);
    }

    public static IpcMessage Deserialize(string json) {
      return JsonSerializer.Deserialize(json, MessageTypeInfo);
    }

    private static JsonSerializerOptions CreateOptions() {
      var options = new JsonSerializerOptions(IpcJsonContext.Default.Options) {
        AllowOutOfOrderMetadataProperties = true,
        TypeInfoResolver = IpcJsonContext.Default.WithAddedModifier(ModifyTypeInfo),
      };
      options.MakeReadOnly();
      return options;
    }

    private static void ModifyTypeInfo(JsonTypeInfo typeInfo) {
      if (typeInfo.Kind != JsonTypeInfoKind.Object) {
        return;
      }

      // Read-only properties: collections are populated when deserializing, computed values are
      // not serialized.
      for (var i = typeInfo.Properties.Count - 1; i >= 0; i--) {
        var property = typeInfo.Properties[i];
        if (property.Set == null) {
          if (IsCollection(property.PropertyType)) {
            property.ObjectCreationHandling = JsonObjectCreationHandling.Populate;
          } else {
            typeInfo.Properties.RemoveAt(i);
          }
        }
      }

      // Polymorphic values: "$type" discriminator with the short class name.
      Type[] derivedTypes;
      if (DerivedTypes.TryGetValue(typeInfo.Type, out derivedTypes)) {
        var polymorphism = new JsonPolymorphismOptions {
          TypeDiscriminatorPropertyName = TypeDiscriminatorPropertyName,
          UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var derivedType in derivedTypes) {
          polymorphism.DerivedTypes.Add(new JsonDerivedType(derivedType, derivedType.Name));
        }
        typeInfo.PolymorphismOptions = polymorphism;
      }
    }

    private static bool IsCollection(Type type) {
      return type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }
  }

  /// <summary>
  /// <see cref="DateTime"/> values are exchanged as UTC (ISO 8601, "Z" suffix).
  /// </summary>
  internal sealed class UtcDateTimeConverter : JsonConverter<DateTime> {
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
      return ToUtc(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) {
      writer.WriteStringValue(ToUtc(value));
    }

    private static DateTime ToUtc(DateTime value) {
      switch (value.Kind) {
        case DateTimeKind.Local:
          return value.ToUniversalTime();
        case DateTimeKind.Unspecified:
          return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        default:
          return value;
      }
    }
  }
}
