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
using ProtoBuf;
using VsChromium.Core.Ipc;

namespace VsChromium.Server.Ipc.Json {
  /// <summary>
  /// Serializes <see cref="IpcMessage"/> instances to/from JSON, as an alternative to the
  /// protobuf-net wire format, for clients that cannot easily speak protobuf-net (e.g. the VS Code
  /// extension).
  ///
  /// The JSON representation mirrors the protobuf-net contract:
  /// <list type="bullet">
  /// <item>Only properties marked with <see cref="ProtoMemberAttribute"/> are serialized.</item>
  /// <item>Property names are camelCase.</item>
  /// <item>Polymorphic values (i.e. types declared with <see cref="ProtoIncludeAttribute"/>) carry a
  /// <c>"$type"</c> property containing the short class name, e.g. <c>"SearchCodeRequest"</c>. Only
  /// [ProtoContract] types of the VsChromium.Core assembly are accepted.</item>
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
        SerializationBinder = new ProtoContractTypesBinder(typeof(IpcMessage).Assembly),
        ContractResolver = new ProtoMembersContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        Converters = { new StringEnumConverter() },
      };
    }

    /// <summary>
    /// Only serialize properties that are part of the protobuf contract.
    /// </summary>
    private class ProtoMembersContractResolver : DefaultContractResolver {
      public ProtoMembersContractResolver() {
        NamingStrategy = new CamelCaseNamingStrategy();
      }

      protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
        return base.CreateProperties(type, memberSerialization)
          .Where(p => p.AttributeProvider.GetAttributes(typeof(ProtoMemberAttribute), true).Count > 0)
          .ToList();
      }
    }

    /// <summary>
    /// Maps "$type" values to [ProtoContract] types of a given assembly, using the short type name.
    /// Any other type name is rejected, so that arbitrary types cannot be instantiated from JSON.
    /// </summary>
    private class ProtoContractTypesBinder : ISerializationBinder {
      private readonly Dictionary<string, Type> _typesByName;

      public ProtoContractTypesBinder(Assembly assembly) {
        _typesByName = assembly.GetTypes()
          .Where(t => t.GetCustomAttribute<ProtoContractAttribute>(false) != null)
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
          throw new JsonSerializationException(string.Format("Type \"{0}\" is not a [ProtoContract] IPC type", serializedType.FullName));
        }
        assemblyName = null;
        typeName = serializedType.Name;
      }
    }
  }
}
