// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Ipc.Json;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestIpcJsonSerializer {
    [TestMethod]
    public void RequestRoundTripWorks() {
      var request = new IpcRequest {
        RequestId = 42,
        Protocol = IpcProtocols.TypedMessage,
        RunOnSequentialQueue = true,
        Data = new SearchCodeRequest {
          SearchParams = new SearchParams {
            SearchString = "foo\nbar \"baz\"",
            FilePathPattern = "*.cc",
            MaxResults = 100,
            MatchCase = true,
            Regex = true,
          }
        }
      };

      var json = IpcJsonSerializer.Serialize(request);
      Assert.IsFalse(json.Contains("\n"), "Serialized messages must fit on a single line");

      var result = IpcJsonSerializer.Deserialize(json) as IpcRequest;
      Assert.IsNotNull(result);
      Assert.AreEqual(42, result.RequestId);
      Assert.AreEqual(IpcProtocols.TypedMessage, result.Protocol);
      Assert.IsTrue(result.RunOnSequentialQueue);
      var data = result.Data as SearchCodeRequest;
      Assert.IsNotNull(data);
      Assert.AreEqual("SearchCodeRequest", data.ClassName);
      Assert.AreEqual("foo\nbar \"baz\"", data.SearchParams.SearchString);
      Assert.AreEqual("*.cc", data.SearchParams.FilePathPattern);
      Assert.AreEqual(100, data.SearchParams.MaxResults);
      Assert.IsTrue(data.SearchParams.MatchCase);
      Assert.IsFalse(data.SearchParams.MatchWholeWord);
      Assert.IsTrue(data.SearchParams.Regex);
    }

    [TestMethod]
    public void PolymorphicValuesHaveTypeDiscriminator() {
      var response = new IpcResponse {
        RequestId = 7,
        Protocol = IpcProtocols.TypedMessage,
        Data = new SearchCodeResponse {
          HitCount = 1,
          SearchResults = new DirectoryEntry {
            Name = "root",
            Entries = {
              new FileEntry {
                Name = "foo.cc",
                Data = new FilePositionsData {
                  Positions = { new FilePositionSpan { Position = 10, Length = 3 } }
                }
              },
              new DirectoryEntry { Name = "subdir" },
            }
          }
        }
      };

      var json = JObject.Parse(IpcJsonSerializer.Serialize(response));
      Assert.AreEqual("IpcResponse", (string)json["$type"]);
      Assert.AreEqual("SearchCodeResponse", (string)json["data"]["$type"]);
      Assert.AreEqual("SearchCodeResponse", (string)json["data"]["className"]);
      // Declared type matches the actual type: no discriminator needed.
      Assert.IsNull(json["data"]["searchResults"]["$type"]);
      var entries = (JArray)json["data"]["searchResults"]["entries"];
      Assert.AreEqual("FileEntry", (string)entries[0]["$type"]);
      Assert.AreEqual("FilePositionsData", (string)entries[0]["data"]["$type"]);
      Assert.AreEqual(10, (int)entries[0]["data"]["positions"][0]["position"]);
      Assert.AreEqual("DirectoryEntry", (string)entries[1]["$type"]);

      // [JsonIgnore] properties are not serialized.
      Assert.IsNull(json["data"]["searchResults"]["isEmpty"]);

      var result = (IpcResponse)IpcJsonSerializer.Deserialize(json.ToString(Formatting.None));
      var results = ((SearchCodeResponse)result.Data).SearchResults;
      Assert.AreEqual(2, results.Entries.Count);
      Assert.IsInstanceOfType(results.Entries[0], typeof(FileEntry));
      Assert.IsInstanceOfType(results.Entries[1], typeof(DirectoryEntry));
      Assert.AreEqual(3, ((FilePositionsData)results.Entries[0].Data).Positions[0].Length);
    }

    [TestMethod]
    public void HandWrittenRequestWithTypeNotFirstWorks() {
      // As sent by a client (e.g. the VS Code extension): "$type" is not necessarily the first
      // property, and default values can be omitted.
      const string json = "{\"requestId\":3,\"protocol\":\"typed-message\"," +
                          "\"data\":{\"fileName\":\"c:\\\\src\\\\foo.txt\",\"$type\":\"RegisterFileRequest\"}," +
                          "\"$type\":\"IpcRequest\"}";
      var result = IpcJsonSerializer.Deserialize(json) as IpcRequest;
      Assert.IsNotNull(result);
      Assert.AreEqual(3, result.RequestId);
      var data = result.Data as RegisterFileRequest;
      Assert.IsNotNull(data);
      Assert.AreEqual(@"c:\src\foo.txt", data.FileName);
    }

    [TestMethod]
    public void EnumsAreSerializedAsStrings() {
      var ipcEvent = new IpcEvent {
        Protocol = IpcProtocols.TypedMessage,
        Data = new IndexingServerStateChangedEvent { ServerStatus = IndexingServerStatus.Busy }
      };
      var json = JObject.Parse(IpcJsonSerializer.Serialize(ipcEvent));
      Assert.AreEqual("IpcEvent", (string)json["$type"]);
      Assert.AreEqual("Busy", (string)json["data"]["serverStatus"]);
    }

    [TestMethod]
    public void UnknownTypesAreRejected() {
      Assert.ThrowsExactly<JsonSerializationException>(() => IpcJsonSerializer.Deserialize("{\"$type\":\"System.IO.FileInfo, mscorlib\",\"requestId\":1}"));
    }

    [TestMethod]
    public void JsonLinesStreamSkipsMalformedLinesAndStopsAtEndOfStream() {
      var input = new StringReader(
        "not json\n" +
        "\n" +
        "{\"$type\":\"IpcRequest\",\"requestId\":1,\"protocol\":\"echo\",\"data\":{\"$type\":\"IpcStringData\",\"text\":\"hi\"}}\n");
      var output = new StringWriter();
      var stream = new IpcStreamOverJsonLines(input, output);

      var request = stream.ReadRequest();
      Assert.IsNotNull(request);
      Assert.AreEqual("hi", ((IpcStringData)request.Data).Text);
      Assert.IsNull(stream.ReadRequest(), "End of stream should return null");

      stream.WriteResponse(new IpcResponse { RequestId = 1, Protocol = "echo", Data = request.Data });
      var lines = output.ToString().Split('\n');
      Assert.AreEqual(2, lines.Length, "One message per line, terminated by a new line");
      Assert.AreEqual("", lines[1]);
      Assert.AreEqual("hi", (string)JObject.Parse(lines[0])["data"]["text"]);
    }
  }
}
