// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VsChromium.Core.Memory;

namespace VsChromium.Server.Cache {
  [Flags]
  public enum IndexCacheEntryFlags : byte {
    None = 0,
    /// <summary>The text contains only ASCII characters.</summary>
    Ascii = 1,
    /// <summary>A binary file: no contents (not searchable), only the time and size.</summary>
    Binary = 2,
  }

  /// <summary>
  /// An entry of the index of the cache: a file, its last write time and size when its contents
  /// were read, and the location of its contents in an arena file.
  /// </summary>
  public readonly struct IndexCacheEntry {
    public IndexCacheEntry(long lastWriteTimeTicks, long fileLength, IndexCacheEntryFlags flags,
                           int arenaId, int offset, int length) {
      LastWriteTimeTicks = lastWriteTimeTicks;
      FileLength = fileLength;
      Flags = flags;
      ArenaId = arenaId;
      Offset = offset;
      Length = length;
    }

    /// <summary>The last write time (UTC ticks) of the file when its contents were read.</summary>
    public long LastWriteTimeTicks { get; }
    /// <summary>The size of the file (on disk) when its contents were read.</summary>
    public long FileLength { get; }
    public IndexCacheEntryFlags Flags { get; }
    /// <summary>The arena file of the contents (text files only).</summary>
    public int ArenaId { get; }
    /// <summary>The offset of the contents in the arena file (aligned).</summary>
    public int Offset { get; }
    /// <summary>The length of the text (UTF-8, without BOM), followed by 2 NUL bytes.</summary>
    public int Length { get; }

    public bool IsBinary => (Flags & IndexCacheEntryFlags.Binary) != 0;
    public bool IsAscii => (Flags & IndexCacheEntryFlags.Ascii) != 0;
  }

  /// <summary>
  /// The contents of <c>index.bin</c> in memory, without an object per entry: the directory paths
  /// and the file names are ranges of a single character buffer, the files an array of structs
  /// (see <see cref="IndexCacheLookup"/> for the lookups by relative path).
  /// </summary>
  public sealed class IndexCacheIndex {
    public struct DirectoryEntry {
      /// <summary>The relative path of the directory ("" for the project root).</summary>
      public int PathStart;
      public int PathLength;
    }

    public struct FileEntry {
      public long LastWriteTimeTicks;
      public long FileLength;
      public int ArenaId;
      public int Offset;
      public int Length;
      public int Directory;
      public int NameStart;
      public ushort NameLength;
      public IndexCacheEntryFlags Flags;

      public IndexCacheEntry ToEntry() {
        return new IndexCacheEntry(LastWriteTimeTicks, FileLength, Flags, ArenaId, Offset, Length);
      }
    }

    public string RootPath;
    public char[] Chars;
    public DirectoryEntry[] Directories;
    public FileEntry[] Files;
    /// <summary>Arena id => bytes used in the arena file.</summary>
    public Dictionary<int, int> Arenas = new Dictionary<int, int>();

    public ReadOnlySpan<char> GetDirectoryPath(int directory) {
      var d = Directories[directory];
      return Chars.AsSpan(d.PathStart, d.PathLength);
    }

    public ReadOnlySpan<char> GetName(int file) {
      var f = Files[file];
      return Chars.AsSpan(f.NameStart, f.NameLength);
    }

    /// <summary>The relative path of a file (tests and diagnostics).</summary>
    public string GetRelativePath(int file) {
      var directory = GetDirectoryPath(Files[file].Directory);
      var name = GetName(file);
      return directory.Length == 0 ? name.ToString() : string.Concat(directory, Core.Files.PathHelpers.DirectorySeparatorChar.ToString(), name);
    }

    /// <summary>The managed memory of the index (approximate).</summary>
    public long ByteSize => (long)Chars.Length * sizeof(char) + (long)Directories.Length * 8 + (long)Files.Length * 40;
  }

  /// <summary>
  /// The on-disk format of the cache of a project:
  /// <list type="bullet">
  /// <item><c>arenas/NNNNNN.arena</c>: the text of the files as stored in memory (UTF-8 without
  /// BOM, followed by 2 NUL bytes, in slots aligned on <see cref="ArenaAllocator.Alignment"/>
  /// bytes), at most <see cref="ArenaSize"/> bytes. Immutable once written: new contents are
  /// written to new arena files (new ids).</item>
  /// <item><c>index.bin</c> (little endian, 7-bit encoded integers, written by <see
  /// cref="IndexCacheFileWriter"/>): header (magic, format version, path case sensitivity, root
  /// path); groups of files of a directory (file count, relative path of the directory front-coded
  /// with the previous one, then per file: name, flags, last write time delta, size, and for text
  /// files arena id delta, offset delta, size minus text length), terminated by a 0 file count;
  /// arena table (id, bytes used); trailer (magic, file count). Written to a temporary file, then
  /// renamed over the previous one.</item>
  /// </list>
  /// </summary>
  public static class IndexCacheFile {
    public const uint HeaderMagic = 0x49435356; // "VSCI"
    public const uint TrailerMagic = 0x45435356; // "VSCE"
    public const int FormatVersion = 2;
    public const int ArenaSize = ArenaAllocator.DefaultArenaSize;
    public const int TrailingByteCount = 2;
    public const string IndexFileName = "index.bin";
    public const string ArenasDirectoryName = "arenas";
    public const string ArenaExtension = ".arena";

    public static string GetArenaFileName(int id) {
      return id.ToString("D6") + ArenaExtension;
    }

    /// <summary>
    /// The id of an arena file name (<see cref="GetArenaFileName"/>), or -1.
    /// </summary>
    public static int ParseArenaFileName(string fileName) {
      if (!fileName.EndsWith(ArenaExtension, StringComparison.OrdinalIgnoreCase))
        return -1;
      var name = fileName.AsSpan(0, fileName.Length - ArenaExtension.Length);
      return int.TryParse(name, System.Globalization.NumberStyles.None, null, out var id) && id > 0 ? id : -1;
    }

    internal static long ZigZag(long value) => (value << 1) ^ (value >> 63);
    internal static long UnZigZag(long value) => (long)((ulong)value >> 1) ^ -(value & 1);

    /// <summary>
    /// Reads and validates an index (<see cref="IndexCacheFileWriter"/>). Throws <see
    /// cref="InvalidDataException"/> (or an I/O exception) if the index is truncated, corrupt, of
    /// another format version, or of another root path or path case sensitivity.
    /// </summary>
    public static IndexCacheIndex Read(Stream stream, string expectedRootPath, bool caseSensitive) {
      using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true)) {
        try {
          return ReadWorker(reader, stream, expectedRootPath, caseSensitive);
        }
        catch (EndOfStreamException e) {
          throw new InvalidDataException("Truncated index", e);
        }
        catch (FormatException e) {
          throw new InvalidDataException("Invalid integer encoding", e);
        }
      }
    }

    private static IndexCacheIndex ReadWorker(BinaryReader reader, Stream stream, string expectedRootPath, bool caseSensitive) {
      if (reader.ReadUInt32() != HeaderMagic)
        throw new InvalidDataException("Invalid header");
      var version = reader.ReadInt32();
      if (version != FormatVersion)
        throw new InvalidDataException($"Format version {version} instead of {FormatVersion}");
      if (reader.ReadBoolean() != caseSensitive)
        throw new InvalidDataException("Different path case sensitivity");
      var index = new IndexCacheIndex { RootPath = reader.ReadString() };
      var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
      if (!string.Equals(index.RootPath, expectedRootPath, comparison))
        throw new InvalidDataException($"Root path \"{index.RootPath}\" instead of \"{expectedRootPath}\"");

      // Note: The names are about half of the index (UTF-8, mostly ASCII).
      var chars = new CharBuffer((int)Math.Clamp(stream.Length / 2, 1024, Array.MaxLength / 4));
      var directories = new ValueList<IndexCacheIndex.DirectoryEntry>(1024);
      var files = new ValueList<IndexCacheIndex.FileEntry>((int)Math.Clamp(stream.Length / 24, 1024, Array.MaxLength / 4));
      var bytes = new byte[1024];
      int previousDirectoryStart = 0, previousDirectoryLength = 0;
      long previousTicks = 0;
      int previousArena = 0, expectedOffset = 0;
      while (true) {
        var fileCount = reader.Read7BitEncodedInt();
        if (fileCount == 0)
          break;
        if (fileCount < 0)
          throw new InvalidDataException("Invalid file count");

        // Directory: shared prefix with the previous directory, then the suffix.
        var prefixLength = reader.Read7BitEncodedInt();
        if (prefixLength < 0 || prefixLength > previousDirectoryLength)
          throw new InvalidDataException("Invalid directory prefix");
        var directoryStart = chars.Length;
        chars.AppendCopy(previousDirectoryStart, prefixLength);
        ReadUtf8(reader, chars, ref bytes);
        previousDirectoryStart = directoryStart;
        previousDirectoryLength = chars.Length - directoryStart;
        directories.Add(new IndexCacheIndex.DirectoryEntry { PathStart = directoryStart, PathLength = previousDirectoryLength });

        for (var i = 0; i < fileCount; i++) {
          var nameStart = chars.Length;
          ReadUtf8(reader, chars, ref bytes);
          var nameLength = chars.Length - nameStart;
          if (nameLength == 0 || nameLength > ushort.MaxValue)
            throw new InvalidDataException("Invalid file name");
          var flags = (IndexCacheEntryFlags)reader.ReadByte();
          var ticks = previousTicks + UnZigZag(reader.Read7BitEncodedInt64());
          previousTicks = ticks;
          var fileLength = reader.Read7BitEncodedInt64();
          if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || fileLength < 0)
            throw new InvalidDataException("Invalid entry");
          var file = new IndexCacheIndex.FileEntry {
            LastWriteTimeTicks = ticks,
            FileLength = fileLength,
            Flags = flags,
            Directory = directories.Count - 1,
            NameStart = nameStart,
            NameLength = (ushort)nameLength,
          };
          if ((flags & IndexCacheEntryFlags.Binary) == 0) {
            var arenaId = previousArena + UnZigZag(reader.Read7BitEncodedInt64());
            var offset = (arenaId == previousArena ? expectedOffset : 0) +
                         UnZigZag(reader.Read7BitEncodedInt64()) * ArenaAllocator.Alignment;
            var length = fileLength - UnZigZag(reader.Read7BitEncodedInt64());
            if (arenaId <= 0 || arenaId > int.MaxValue || offset < 0 || offset > ArenaSize || length < 0 || length > ArenaSize)
              throw new InvalidDataException("Invalid location");
            file.ArenaId = (int)arenaId;
            file.Offset = (int)offset;
            file.Length = (int)length;
            previousArena = file.ArenaId;
            expectedOffset = file.Offset + ArenaAllocator.GetSlotSize(file.Length + TrailingByteCount);
          }
          files.Add(file);
        }
      }

      var arenaCount = reader.Read7BitEncodedInt();
      if (arenaCount < 0)
        throw new InvalidDataException("Invalid arena count");
      for (var i = 0; i < arenaCount; i++) {
        var id = reader.Read7BitEncodedInt();
        var used = reader.Read7BitEncodedInt();
        if (id <= 0 || used <= 0 || used > ArenaSize || !index.Arenas.TryAdd(id, used))
          throw new InvalidDataException("Invalid arena table");
      }

      if (reader.ReadUInt32() != TrailerMagic || reader.ReadInt32() != files.Count)
        throw new InvalidDataException("Invalid trailer");
      if (stream.Position != stream.Length)
        throw new InvalidDataException("Unexpected data after the trailer");

      // The contents must be in the used bytes of their arena file.
      var fileArray = files.ToArray();
      foreach (var file in fileArray) {
        if ((file.Flags & IndexCacheEntryFlags.Binary) == 0 &&
            (!index.Arenas.TryGetValue(file.ArenaId, out var used) ||
             (long)file.Offset + file.Length + TrailingByteCount > used))
          throw new InvalidDataException("Invalid location");
      }

      index.Chars = chars.ToArray();
      index.Directories = directories.ToArray();
      index.Files = fileArray;
      return index;
    }

    /// <summary>
    /// Reads a string written by <see cref="BinaryWriter.Write(string)"/> (7-bit encoded byte
    /// count, UTF-8 bytes) into <paramref name="chars"/>, without allocating a string.
    /// </summary>
    private static void ReadUtf8(BinaryReader reader, CharBuffer chars, ref byte[] bytes) {
      var byteCount = reader.Read7BitEncodedInt();
      if (byteCount < 0 || byteCount > 1024 * 1024)
        throw new InvalidDataException("Invalid string length");
      if (bytes.Length < byteCount)
        bytes = new byte[byteCount];
      var span = bytes.AsSpan(0, byteCount);
      for (var read = 0; read < byteCount;) {
        var n = reader.Read(span.Slice(read));
        if (n == 0)
          throw new EndOfStreamException();
        read += n;
      }
      chars.EnsureCapacity(Encoding.UTF8.GetMaxCharCount(byteCount));
      chars.Length += Encoding.UTF8.GetChars(span, chars.FreeSpan);
    }

    /// <summary>A growable buffer of characters.</summary>
    private sealed class CharBuffer {
      private char[] _chars;
      public int Length;

      public CharBuffer(int capacity) {
        _chars = new char[capacity];
      }

      public Span<char> FreeSpan => _chars.AsSpan(Length);

      public void EnsureCapacity(int additional) {
        if (Length + additional > _chars.Length) {
          Array.Resize(ref _chars, (int)Math.Min(Math.Max(Length + additional, _chars.Length * 2L), Array.MaxLength));
        }
      }

      /// <summary>Appends a copy of a range of this buffer.</summary>
      public void AppendCopy(int start, int length) {
        EnsureCapacity(length);
        _chars.AsSpan(start, length).CopyTo(_chars.AsSpan(Length));
        Length += length;
      }

      public char[] ToArray() {
        return _chars.AsSpan(0, Length).ToArray();
      }
    }

    /// <summary>A growable array of structs (exposes the count without a List wrapper).</summary>
    private struct ValueList<T> {
      private T[] _items;
      public int Count;

      public ValueList(int capacity) {
        _items = new T[capacity];
        Count = 0;
      }

      public void Add(in T item) {
        if (Count == _items.Length) {
          Array.Resize(ref _items, (int)Math.Min(_items.Length * 2L, Array.MaxLength));
        }
        _items[Count++] = item;
      }

      public T[] ToArray() {
        return _items.AsSpan(0, Count).ToArray();
      }
    }
  }

  /// <summary>
  /// Writes <c>index.bin</c> (see <see cref="IndexCacheFile"/>) as the entries are produced (no
  /// list of entries in memory): the files of a directory must be consecutive.
  /// </summary>
  public sealed class IndexCacheFileWriter : IDisposable {
    private readonly BinaryWriter _writer;
    private readonly MemoryStream _group = new MemoryStream();
    private readonly BinaryWriter _groupWriter;
    private string _groupDirectory;
    private int _groupFileCount;
    private string _previousDirectory = "";
    private long _previousTicks;
    private int _previousArena;
    private int _expectedOffset;

    public IndexCacheFileWriter(Stream stream, string rootPath, bool caseSensitive) {
      _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
      _groupWriter = new BinaryWriter(_group, Encoding.UTF8, leaveOpen: true);
      _writer.Write(IndexCacheFile.HeaderMagic);
      _writer.Write(IndexCacheFile.FormatVersion);
      _writer.Write(caseSensitive);
      _writer.Write(rootPath);
    }

    public int FileCount { get; private set; }

    /// <summary>
    /// Adds a file of <paramref name="directory"/> (relative path, "" for the project root).
    /// </summary>
    public void Add(string directory, string name, in IndexCacheEntry entry) {
      if (!ReferenceEquals(directory, _groupDirectory) && directory != _groupDirectory) {
        FlushGroup();
        _groupDirectory = directory;
      }
      var w = _groupWriter;
      w.Write(name);
      w.Write((byte)entry.Flags);
      w.Write7BitEncodedInt64(IndexCacheFile.ZigZag(entry.LastWriteTimeTicks - _previousTicks));
      _previousTicks = entry.LastWriteTimeTicks;
      w.Write7BitEncodedInt64(entry.FileLength);
      if (!entry.IsBinary) {
        w.Write7BitEncodedInt64(IndexCacheFile.ZigZag(entry.ArenaId - (long)_previousArena));
        var expected = entry.ArenaId == _previousArena ? _expectedOffset : 0;
        w.Write7BitEncodedInt64(IndexCacheFile.ZigZag((entry.Offset - expected) / ArenaAllocator.Alignment));
        w.Write7BitEncodedInt64(IndexCacheFile.ZigZag(entry.FileLength - entry.Length));
        _previousArena = entry.ArenaId;
        _expectedOffset = entry.Offset + ArenaAllocator.GetSlotSize(entry.Length + IndexCacheFile.TrailingByteCount);
      }
      _groupFileCount++;
      FileCount++;
    }

    private void FlushGroup() {
      if (_groupFileCount == 0)
        return;
      _groupWriter.Flush();
      _writer.Write7BitEncodedInt(_groupFileCount);
      var prefix = _previousDirectory.AsSpan().CommonPrefixLength(_groupDirectory);
      _writer.Write7BitEncodedInt(prefix);
      _writer.Write(_groupDirectory.Substring(prefix));
      _writer.Write(_group.GetBuffer(), 0, (int)_group.Length);
      _previousDirectory = _groupDirectory;
      _group.SetLength(0);
      _groupFileCount = 0;
    }

    /// <summary>
    /// Writes the end of the index: the arena table (id => bytes used) and the trailer.
    /// </summary>
    public void Finish(IReadOnlyDictionary<int, int> arenas) {
      FlushGroup();
      _writer.Write7BitEncodedInt(0);
      _writer.Write7BitEncodedInt(arenas.Count);
      foreach (var (id, used) in arenas) {
        _writer.Write7BitEncodedInt(id);
        _writer.Write7BitEncodedInt(used);
      }
      _writer.Write(IndexCacheFile.TrailerMagic);
      _writer.Write(FileCount);
      _writer.Flush();
    }

    public void Dispose() {
      _groupWriter.Dispose();
      _writer.Dispose();
    }
  }
}
