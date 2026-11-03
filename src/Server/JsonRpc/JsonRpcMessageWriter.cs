// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Buffers;
using System.Globalization;
using System.IO;

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// A message being written (e.g. with a <see cref="System.Text.Json.Utf8JsonWriter"/>): the JSON
  /// content is written to a buffer rented from <see cref="ArrayPool{T}.Shared"/>, after space
  /// reserved for the header, so that the framed message (header + content) is written to the
  /// stream with a single call, without copy. Dispose to return the buffer to the pool.
  /// </summary>
  public sealed class JsonRpcMessageBuffer : IBufferWriter<byte>, IDisposable {
    /// <summary>"Content-Length: " + 10 digits + "\r\n\r\n".</summary>
    private const int HeaderReserve = 32;
    private const int InitialSize = 4096;

    private byte[] _buffer;
    private int _position;

    public JsonRpcMessageBuffer() {
      _buffer = ArrayPool<byte>.Shared.Rent(InitialSize);
      _position = HeaderReserve;
    }

    /// <summary>The JSON content written so far.</summary>
    public ReadOnlySpan<byte> Content => _buffer.AsSpan(HeaderReserve, _position - HeaderReserve);

    public void Advance(int count) {
      if (count < 0 || _position + count > _buffer.Length) {
        throw new ArgumentOutOfRangeException(nameof(count));
      }
      _position += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0) {
      EnsureCapacity(sizeHint);
      return _buffer.AsMemory(_position);
    }

    public Span<byte> GetSpan(int sizeHint = 0) {
      EnsureCapacity(sizeHint);
      return _buffer.AsSpan(_position);
    }

    /// <summary>
    /// Writes the header in front of the content and returns the whole message.
    /// </summary>
    public ReadOnlySpan<byte> GetFramedMessage() {
      Span<byte> header = stackalloc byte[HeaderReserve];
      "Content-Length: "u8.CopyTo(header);
      var length = "Content-Length: "u8.Length;
      (_position - HeaderReserve).TryFormat(header.Slice(length), out var digits, default, CultureInfo.InvariantCulture);
      length += digits;
      "\r\n\r\n"u8.CopyTo(header.Slice(length));
      length += 4;
      var start = HeaderReserve - length;
      header.Slice(0, length).CopyTo(_buffer.AsSpan(start));
      return _buffer.AsSpan(start, _position - start);
    }

    public void Dispose() {
      var buffer = _buffer;
      _buffer = null;
      if (buffer != null) {
        ArrayPool<byte>.Shared.Return(buffer);
      }
    }

    private void EnsureCapacity(int sizeHint) {
      if (_buffer == null) {
        throw new ObjectDisposedException(nameof(JsonRpcMessageBuffer));
      }
      var needed = _position + Math.Max(sizeHint, 1);
      if (needed <= _buffer.Length) {
        return;
      }
      var newSize = (int)Math.Min(Math.Max((long)_buffer.Length * 2, needed), Array.MaxLength);
      if (newSize < needed) {
        throw new OutOfMemoryException("JSON-RPC message too large");
      }
      var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
      Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _position);
      ArrayPool<byte>.Shared.Return(_buffer);
      _buffer = newBuffer;
    }
  }

  /// <summary>
  /// Writes framed messages (see <see cref="JsonRpcMessageReader"/>) to a stream. Thread safe: each
  /// message is written with a single call to the stream, under a lock, then the stream is flushed.
  /// </summary>
  public sealed class JsonRpcMessageWriter {
    private readonly object _lock = new object();
    private readonly Stream _stream;

    public JsonRpcMessageWriter(Stream stream) {
      _stream = stream;
    }

    public void WriteMessage(JsonRpcMessageBuffer message) {
      var bytes = message.GetFramedMessage();
      lock (_lock) {
        _stream.Write(bytes);
        _stream.Flush();
      }
    }

    /// <summary>Writes a message whose JSON content is <paramref name="content"/>.</summary>
    public void WriteMessage(ReadOnlySpan<byte> content) {
      using (var message = new JsonRpcMessageBuffer()) {
        content.CopyTo(message.GetSpan(content.Length));
        message.Advance(content.Length);
        WriteMessage(message);
      }
    }
  }
}
