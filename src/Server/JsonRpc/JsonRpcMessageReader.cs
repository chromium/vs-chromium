// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// Error in the framing of the messages (headers): the stream can't be read any further.
  /// </summary>
  public class JsonRpcFramingException : IOException {
    public JsonRpcFramingException(string message) : base(message) {
    }
  }

  /// <summary>
  /// Reads the messages of a stream framed as in the Language Server Protocol: headers
  /// (<c>Content-Length: N</c>, other headers such as <c>Content-Type</c> are ignored, header names
  /// are case-insensitive), each terminated by "\r\n", an empty line, then N bytes of UTF-8 JSON.
  /// Not thread safe: messages are read by a single thread.
  /// </summary>
  public sealed class JsonRpcMessageReader {
    private const string ContentLengthHeader = "Content-Length";
    /// <summary>Maximum length of a header line (protection against garbage input).</summary>
    private const int MaxHeaderLineLength = 8 * 1024;

    private readonly Stream _stream;
    private readonly byte[] _buffer;
    /// <summary>Start of the bytes of <see cref="_buffer"/> not consumed yet.</summary>
    private int _start;
    /// <summary>End of the bytes read in <see cref="_buffer"/>.</summary>
    private int _end;

    public JsonRpcMessageReader(Stream stream, int bufferSize = 64 * 1024) {
      _stream = stream;
      _buffer = new byte[Math.Max(bufferSize, MaxHeaderLineLength)];
    }

    /// <summary>
    /// Reads the next message and returns its content (the JSON bytes), or returns null when the
    /// end of the stream is reached between two messages. Throws <see
    /// cref="JsonRpcFramingException"/> if the headers are invalid or the stream ends in the middle
    /// of a message.
    /// </summary>
    public byte[] ReadMessage() {
      var contentLength = -1;
      var headerCount = 0;
      while (true) {
        var line = ReadHeaderLine();
        if (line == null) {
          if (headerCount == 0) {
            return null;
          }
          throw new JsonRpcFramingException("Unexpected end of stream in the headers of a message");
        }

        if (line.Length == 0) {
          if (headerCount == 0) {
            // Tolerate empty lines between messages.
            continue;
          }
          break;
        }

        headerCount++;
        var separator = line.IndexOf(':');
        if (separator <= 0) {
          throw new JsonRpcFramingException($"Invalid message header \"{Truncate(line)}\"");
        }
        var name = line.Substring(0, separator).Trim();
        var value = line.Substring(separator + 1).Trim();
        if (string.Equals(name, ContentLengthHeader, StringComparison.OrdinalIgnoreCase)) {
          if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength)) {
            throw new JsonRpcFramingException($"Invalid Content-Length header value \"{Truncate(value)}\"");
          }
        }
        // Other headers (e.g. "Content-Type") are ignored.
      }

      if (contentLength < 0) {
        throw new JsonRpcFramingException("Missing Content-Length header");
      }
      return ReadContent(contentLength);
    }

    private byte[] ReadContent(int length) {
      var content = new byte[length];
      var buffered = Math.Min(length, _end - _start);
      Buffer.BlockCopy(_buffer, _start, content, 0, buffered);
      _start += buffered;
      var offset = buffered;
      while (offset < length) {
        var count = _stream.Read(content, offset, length - offset);
        if (count == 0) {
          throw new JsonRpcFramingException($"Unexpected end of stream in a message ({offset:n0} bytes of {length:n0})");
        }
        offset += count;
      }
      return content;
    }

    /// <summary>
    /// Returns the next header line without its line terminator ("\r\n", or "\n"), or null at the
    /// end of the stream if no byte of the line was read.
    /// </summary>
    private string ReadHeaderLine() {
      var searchStart = _start;
      while (true) {
        var index = Array.IndexOf(_buffer, (byte)'\n', searchStart, _end - searchStart);
        if (index >= 0) {
          var length = index - _start;
          if (length > 0 && _buffer[index - 1] == '\r') {
            length--;
          }
          var line = Encoding.ASCII.GetString(_buffer, _start, length);
          _start = index + 1;
          return line;
        }

        if (_end - _start >= MaxHeaderLineLength) {
          throw new JsonRpcFramingException($"Message header longer than {MaxHeaderLineLength:n0} bytes");
        }

        // Move the partial line to the beginning of the buffer, and read more bytes.
        if (_start > 0) {
          Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
          _end -= _start;
          _start = 0;
        }
        searchStart = _end;
        var count = _stream.Read(_buffer, _end, _buffer.Length - _end);
        if (count == 0) {
          if (_end == _start) {
            return null;
          }
          throw new JsonRpcFramingException("Unexpected end of stream in the headers of a message");
        }
        _end += count;
      }
    }

    private static string Truncate(string value) {
      return value.Length <= 100 ? value : value.Substring(0, 100) + "...";
    }
  }
}
