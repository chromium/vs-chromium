// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// The pieces of text searched by code searches (see <see cref="FileContentsPiece"/>), in a
  /// compact form: one integer per small file (most files), one small entry per chunk of large
  /// files. Pieces are created on access from the arrays of the file database.
  /// <para>
  /// Small files (a single piece: their whole contents) are identified by their index in the
  /// arrays of the file database: their contents are read from the contents array when the piece
  /// is accessed, so that an edit of a small file (its contents slot is replaced) doesn't require
  /// new pieces. The chunks of large files keep their contents (the chunk ranges depend on them).
  /// </para>
  /// </summary>
  public sealed class FilePieceList : IList<FileContentsPiece>, IReadOnlyList<FileContentsPiece> {
    private readonly FileName[] _fileNames;
    private readonly FileContents[] _contents;
    /// <summary>
    /// Each item is either a file index (<c>&gt;= 0</c>, a small file) or the complement of an
    /// index in <see cref="_chunks"/> (<c>&lt; 0</c>).
    /// </summary>
    private readonly int[] _items;
    private readonly Chunk[] _chunks;

    public FilePieceList(FileName[] fileNames, FileContents[] contents, int[] items, Chunk[] chunks) {
      _fileNames = fileNames;
      _contents = contents;
      _items = items;
      _chunks = chunks;
    }

    public static readonly FilePieceList Empty = new FilePieceList(
      Array.Empty<FileName>(), Array.Empty<FileContents>(), Array.Empty<int>(), Array.Empty<Chunk>());

    public int Count => _items.Length;

    /// <summary>
    /// Memory used by this list (diagnostics).
    /// </summary>
    public long ByteSize => _items.Length * 4L + _chunks.Length * 24L;

    public FileContentsPiece this[int index] {
      get {
        var item = _items[index];
        if (item >= 0) {
          var contents = _contents[item];
          // Note: An edited small file may have become large: its trigram filters (if any)
          // have several chunks, the whole text is searched without filtering (chunk -1).
          var chunkIndex = contents.ByteLength <= FileContents.MaxChunkLength ? 0 : -1;
          return new FileContentsPiece(_fileNames[item], contents, item, contents.TextRange, chunkIndex);
        }
        var chunk = _chunks[~item];
        return new FileContentsPiece(_fileNames[chunk.FileIndex], chunk.Contents, chunk.FileIndex, chunk.Range, chunk.ChunkIndex);
      }
      set => throw new NotSupportedException();
    }

    public IEnumerator<FileContentsPiece> GetEnumerator() {
      for (var i = 0; i < _items.Length; i++) {
        yield return this[i];
      }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool IsReadOnly => true;

    public void CopyTo(FileContentsPiece[] array, int arrayIndex) {
      for (var i = 0; i < _items.Length; i++) {
        array[arrayIndex + i] = this[i];
      }
    }

    public int IndexOf(FileContentsPiece item) => throw new NotSupportedException();
    public bool Contains(FileContentsPiece item) => throw new NotSupportedException();
    public void Insert(int index, FileContentsPiece item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void Add(FileContentsPiece item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Remove(FileContentsPiece item) => throw new NotSupportedException();

    /// <summary>
    /// A chunk of a large file.
    /// </summary>
    public readonly struct Chunk {
      public Chunk(int fileIndex, int chunkIndex, TextRange range, FileContents contents) {
        FileIndex = fileIndex;
        ChunkIndex = chunkIndex;
        Range = range;
        Contents = contents;
      }

      public int FileIndex { get; }
      public int ChunkIndex { get; }
      public TextRange Range { get; }
      public FileContents Contents { get; }
    }
  }
}
