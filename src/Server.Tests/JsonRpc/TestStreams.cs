// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace VsChromium.Tests.JsonRpc {
  /// <summary>
  /// An in-memory pipe: bytes written to the stream are read from it, in order. Reads block until
  /// bytes are available or the writer side is completed (<see cref="CompleteWriting"/>: end of
  /// stream). Reads time out (<see cref="TimeoutException"/>) after <see cref="ReadTimeout"/> so
  /// that tests never hang.
  /// </summary>
  public sealed class PipeStream : Stream {
    private readonly object _lock = new object();
    private readonly Queue<byte[]> _chunks = new Queue<byte[]>();
    private byte[] _current;
    private int _currentOffset;
    private bool _completed;

    public override int ReadTimeout { get; set; } = 30_000;
    public override bool CanTimeout => true;

    public void CompleteWriting() {
      lock (_lock) {
        _completed = true;
        Monitor.PulseAll(_lock);
      }
    }

    public override int Read(byte[] buffer, int offset, int count) {
      if (count == 0) {
        return 0;
      }
      lock (_lock) {
        var deadline = DateTime.UtcNow.AddMilliseconds(ReadTimeout);
        while (_current == null) {
          if (_chunks.Count > 0) {
            _current = _chunks.Dequeue();
            _currentOffset = 0;
            break;
          }
          if (_completed) {
            return 0;
          }
          var remaining = deadline - DateTime.UtcNow;
          if (remaining <= TimeSpan.Zero) {
            throw new TimeoutException("Timeout reading from the pipe");
          }
          Monitor.Wait(_lock, remaining);
        }
        var length = Math.Min(count, _current.Length - _currentOffset);
        Buffer.BlockCopy(_current, _currentOffset, buffer, offset, length);
        _currentOffset += length;
        if (_currentOffset == _current.Length) {
          _current = null;
        }
        return length;
      }
    }

    public override void Write(byte[] buffer, int offset, int count) {
      if (count == 0) {
        return;
      }
      var chunk = new byte[count];
      Buffer.BlockCopy(buffer, offset, chunk, 0, count);
      lock (_lock) {
        if (_completed) {
          throw new IOException("The pipe is closed");
        }
        _chunks.Enqueue(chunk);
        Monitor.PulseAll(_lock);
      }
    }

    public override void Flush() {
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
  }

  /// <summary>
  /// A read-only stream over bytes, whose reads return at most <c>chunkSize</c> bytes (to test
  /// messages split across reads).
  /// </summary>
  public sealed class ChunkedStream : Stream {
    private readonly byte[] _bytes;
    private readonly int _chunkSize;
    private int _position;

    public ChunkedStream(byte[] bytes, int chunkSize) {
      _bytes = bytes;
      _chunkSize = chunkSize;
    }

    public override int Read(byte[] buffer, int offset, int count) {
      var length = Math.Min(Math.Min(count, _chunkSize), _bytes.Length - _position);
      Buffer.BlockCopy(_bytes, _position, buffer, offset, length);
      _position += length;
      return length;
    }

    public override void Flush() {
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _bytes.Length;
    public override long Position {
      get => _position;
      set => throw new NotSupportedException();
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  }
}
