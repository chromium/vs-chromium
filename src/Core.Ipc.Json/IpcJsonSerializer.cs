// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using VsChromium.Core.Ipc;

namespace VsChromium.Core.Ipc.Json {
  /// <summary>
  /// Serializes <see cref="IpcMessage"/> instances to/from JSON. This is the protocol between the
  /// clients (Visual Studio package, VS Code extension) and the server, over the server standard
  /// input/output (see <see cref="IpcStreamOverJsonLines"/>).
  ///
  /// The JSON representation:
  /// <list type="bullet">
  /// <item>The public read/write properties of the message classes are serialized, as well as
  /// read-only collection properties (populated when deserializing). Other read-only properties
  /// (computed values, e.g. <c>DirectoryEntry.IsEmpty</c>) are not.</item>
  /// <item>Property names are camelCase.</item>
  /// <item>Polymorphic values (i.e. values of a sub-class of the declared type of a property) carry a
  /// <c>"$type"</c> property containing the short class name, e.g. <c>"SearchCodeRequest"</c>. Only
  /// the message classes (public classes of the <c>VsChromium.Core.Ipc</c> and
  /// <c>VsChromium.Core.Ipc.TypedMessages</c> namespaces of this assembly) are accepted.</item>
  /// <item>Enums are serialized as strings.</item>
  /// </list>
  /// </summary>
  public static class IpcJsonSerializer {
    private static readonly JsonSerializerSettings Settings = CreateSettings();

    public static string Serialize(IpcMessage message) {
      // Passing the root type ensures the "$type" of the root object is always emitted.
      return JsonConvert.SerializeObject(message, typeof(IpcMessage), Settings);
    }

    public static IpcMessage Deserialize(string json) {
      return JsonConvert.DeserializeObject<IpcMessage>(json, Settings);
    }

    private static JsonSerializerSettings CreateSettings() {
      return new JsonSerializerSettings {
        Formatting = Formatting.None,
        TypeNameHandling = TypeNameHandling.Auto,
        // Allow "$type" to appear anywhere in an object (not only as the first property).
        MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
        SerializationBinder = new MessageTypesBinder(typeof(IpcMessage).Assembly),
        ContractResolver = new MessageContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        Converters = { new StringEnumConverter() },
      };
    }

    /// <summary>
    /// camelCase property names; read/write properties and read-only collection properties only.
    /// </summary>
    private class MessageContractResolver : DefaultContractResolver {
      public MessageContractResolver() {
        NamingStrategy = new CamelCaseNamingStrategy();
      }

      protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        var property = base.CreateProperty(member, memberSerialization);
        if (!property.Writable && !IsCollection(property.PropertyType)) {
          property.Ignored = true;
        }
        return property;
      }

      private static bool IsCollection(Type type) {
        return type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
      }
    }

    /// <summary>
    /// Maps "$type" values to the message classes of a given assembly, using the short type name.
    /// Any other type name is rejected, so that arbitrary types cannot be instantiated from JSON.
    /// </summary>
    private class MessageTypesBinder : ISerializationBinder {
      private static readonly string[] MessageNamespaces = {
        typeof(IpcMessage).Namespace,
        typeof(TypedMessages.TypedMessage).Namespace,
      };

      private readonly Dictionary<string, Type> _typesByName;

      public MessageTypesBinder(Assembly assembly) {
        _typesByName = assembly.GetTypes()
          .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract && !typeof(Exception).IsAssignableFrom(t))
          .Where(t => MessageNamespaces.Contains(t.Namespace))
          .ToDictionary(t => t.Name, t => t, StringComparer.Ordinal);
      }

      public Type BindToType(string assemblyName, string typeName) {
        Type type;
        if (assemblyName == null && _typesByName.TryGetValue(typeName, out type)) {
          return type;
        }
        throw new JsonSerializationException(string.Format("Unknown or unsupported \"$type\" value: \"{0}\"", typeName));
      }

      public void BindToName(Type serializedType, out string assemblyName, out string typeName) {
        if (!_typesByName.ContainsKey(serializedType.Name)) {
          throw new JsonSerializationException(string.Format("Type \"{0}\" is not an IPC message type", serializedType.FullName));
        }
        assemblyName = null;
        typeName = serializedType.Name;
      }
    }
  }
}
