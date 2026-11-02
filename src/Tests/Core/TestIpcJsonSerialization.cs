// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.Json;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;

namespace VsChromium.Tests.Core {
  [TestClass]
  /// <summary>
  /// Tests of the JSON serialization of the IPC messages (the protocol between
  /// the Visual Studio package and the server), on the Visual Studio side
  /// (.NET Framework, Newtonsoft.Json of Visual Studio).
  /// </summary>
  public class TestIpcJsonSerialization : MefTestBase {
    [TestMethod]
    public void JsonSerializationWorksForStringData() {
      using (var container = SetupMefContainer()) {

        var req = new IpcRequest {
          RequestId = 6,
          Protocol = IpcProtocols.TypedMessage,
          Data = new IpcStringData {
            Text = "c:\\hhhh"
          }
        };

        AssertRoundTrip(req);
      }
    }

    [TestMethod]
    public void JsonSerializationWorksForRegisterFileRequest() {
      using (var container = SetupMefContainer()) {

        var req = new IpcRequest {
          RequestId = 7,
          Protocol = IpcProtocols.TypedMessage,
          Data = new RegisterFileRequest {
            FileName = "c:\\hhhh"
          }
        };

        AssertRoundTrip(req);
      }
    }

    [TestMethod]
    public void JsonSerializationWorksForHelloProtocol() {
      using (var container = SetupMefContainer()) {
        var req = HelloWorldProtocol.Request;
        AssertRoundTrip(req);
      }
    }

    [TestMethod]
    public void JsonSerializationWorksForAllIpcMessageTypes() {
      using (var container = SetupMefContainer()) {
        Func<Type, Type, bool> isSubTypeOf = (x, y) => {
          for (; x != null; x = x.BaseType) {
            if (x == y)
              return true;
          }
          return false;
        };

        var ipcTypes = typeof(IpcMessage).Assembly.GetTypes()
          .Where(x => x.IsClass)
          .Where(x => isSubTypeOf(x, typeof(IpcMessage)));

        var ipcDataTypes = typeof(IpcMessageData).Assembly.GetTypes()
          .Where(x => x.IsClass)
          .Where(x => isSubTypeOf(x, typeof(IpcMessageData)))
          .ToList();

        foreach (var ipcType in ipcTypes) {
          foreach (var ipcDataType in ipcDataTypes) {
            Trace.WriteLine(string.Format("Serializing IPC type \"{0}\" with IPC Message Data Type \"{1}\".",
                                          ipcType.Name, ipcDataType.Name));
            var ipcValue = (IpcMessage)Activator.CreateInstance(ipcType);
            var ipcDataValue = (IpcMessageData)Activator.CreateInstance(ipcDataType);
            ipcValue.Data = ipcDataValue;
            AssertRoundTrip(ipcValue);
          }
        }
      }
    }

    [TestMethod]
    public void JsonSerializationWorksForBigRequest() {
      Logger.LogMemoryStats();
      using (var container = SetupMefContainer()) {
        var req = new IpcRequest {
          RequestId = 6,
          Protocol = IpcProtocols.TypedMessage,
          Data = new GetFileSystemResponse {
            Tree = CreateBigFileSystemTree()
          }
        };
        Logger.LogMemoryStats();
        AssertRoundTrip(req);
      }
      Logger.LogMemoryStats();
    }

    private FileSystemTree_Obsolete CreateBigFileSystemTree() {
      var sw = Stopwatch.StartNew();
      var root = new DirectoryEntry();
      CreateBigFileSystem(root, 0);
      sw.Stop();
      Trace.WriteLine(string.Format("Create a tree with {0:n0} elements in {1} msec.", CountTreeSize(root),
                                    sw.ElapsedMilliseconds));
      return new FileSystemTree_Obsolete {
        Version = 1,
        Root = root
      };
    }

    private long CountTreeSize(DirectoryEntry root) {
      long result = 1;
      result += root.Entries.OfType<FileEntry>().Count();
      result += root.Entries.OfType<DirectoryEntry>().Aggregate(0L, (n, x) => n + CountTreeSize(x));
      return result;
    }

    private void CreateBigFileSystem(DirectoryEntry parent, int depth) {
      for (var i = 0; i < 200; i++) {
        var file = new FileEntry {
          Name = string.Format("File-{0}", i)
        };
        parent.Entries.Add(file);
      }

      if (depth < 3) {
        for (var i = 0; i < 10; i++) {
          var directory = new DirectoryEntry {
            Name = string.Format("Folder-{0}", i)
          };
          parent.Entries.Add(directory);
          CreateBigFileSystem(directory, depth + 1);
        }
      }
    }

    private static void AssertRoundTrip(IpcMessage message) {
      var result = RoundTrip(message);
      Assert.AreEqual(message.GetType(), result.GetType());
      Assert.AreEqual(message.RequestId, result.RequestId);
      Assert.AreEqual(message.Protocol, result.Protocol);
      Assert.AreEqual(message.Data == null, result.Data == null);
      if (message.Data != null) {
        Assert.AreEqual(message.Data.GetType(), result.Data.GetType());
      }
    }

    private static T RoundTrip<T>(T req) where T : IpcMessage {
      var sw = Stopwatch.StartNew();
      var json = IpcJsonSerializer.Serialize(req);
      sw.Stop();
      Trace.WriteLine(string.Format("Serialized message of type {0} into {1:n0} characters in {2} msec.",
                                    req.Data.GetType().FullName, json.Length, sw.ElapsedMilliseconds));

      sw.Restart();
      var result = IpcJsonSerializer.Deserialize(json);
      sw.Stop();
      Trace.WriteLine(string.Format("Deserialized message of type {0} from {1:n0} characters in {2} msec.",
                                    req.Data.GetType().FullName, json.Length, sw.ElapsedMilliseconds));
      return (T)result;
    }  }
}
