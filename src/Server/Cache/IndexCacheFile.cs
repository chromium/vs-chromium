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
  /// An entry of the index of the cache: a file (path relative to the project root, in the
  /// dictionary key), its last write time and size when its contents were read, and the location
  /// of its contents in an arena file.
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
  /// The contents of <c>index.bin</c>: the arena files and the entries.
  /// </summary>
  public sealed class IndexCacheData {
    public string RootPath { get; set; }
    /// <summary>Arena id => bytes used in the arena file.</summary>
    public Dictionary<int, int> Arenas { get; set; } = new Dictionary<int, int>();
    public List<KeyValuePair<string, IndexCacheEntry>> Entries { get; set; } = new List<KeyValuePair<string, IndexCacheEntry>>();
  }

  /// <summary>
  /// The on-disk format of the cache of a project:
  /// <list type="bullet">
  /// <item><c>arenas/NNNNNN.arena</c>: the text of the files as stored in memory (UTF-8 without
  /// BOM, followed by 2 NUL bytes, in slots aligned on <see cref="ArenaAllocator.Alignment"/>
  /// bytes), at most <see cref="ArenaSize"/> bytes. Immutable once written: new contents are
  /// written to new arena files (new ids).</item>
  /// <item><c>index.bin</c> (little endian, <see cref="BinaryWriter"/>): header (magic, format
  /// version, path case sensitivity, root path), arena table (id, bytes used), entries (relative
  /// path, last write time, size, flags, and for text files arena id, offset, length), trailer
  /// (magic, entry count). Written to a temporary file, then renamed over the previous one.</item>
  /// </list>
  /// </summary>
  public static class IndexCacheFile {
    public const uint HeaderMagic = 0x49435356; // "VSCI"
    public const uint TrailerMagic = 0x45435356; // "VSCE"
    public const int FormatVersion = 1;
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

    public static void Write(Stream stream, IndexCacheData data, bool caseSensitive) {
      using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
        writer.Write(HeaderMagic);
        writer.Write(FormatVersion);
        writer.Write(caseSensitive);
        writer.Write(data.RootPath);

        writer.Write(data.Arenas.Count);
        foreach (var arena in data.Arenas) {
          writer.Write(arena.Key);
          writer.Write(arena.Value);
        }

        writer.Write(data.Entries.Count);
        foreach (var item in data.Entries) {
          var entry = item.Value;
          writer.Write(item.Key);
          writer.Write(entry.LastWriteTimeTicks);
          writer.Write(entry.FileLength);
          writer.Write((byte)entry.Flags);
          if (!entry.IsBinary) {
            writer.Write(entry.ArenaId);
            writer.Write(entry.Offset);
            writer.Write(entry.Length);
          }
        }

        writer.Write(TrailerMagic);
        writer.Write(data.Entries.Count);
      }
    }

    /// <summary>
    /// Reads and validates an index (<see cref="Write"/>). Throws <see cref="InvalidDataException"/>
    /// (or an I/O exception) if the index is truncated, corrupt, of another format version, or of
    /// another root path or path case sensitivity.
    /// </summary>
    public static IndexCacheData Read(Stream stream, string expectedRootPath, bool caseSensitive) {
      using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true)) {
        try {
          if (reader.ReadUInt32() != HeaderMagic)
            throw new InvalidDataException("Invalid header");
          var version = reader.ReadInt32();
          if (version != FormatVersion)
            throw new InvalidDataException($"Format version {version} instead of {FormatVersion}");
          if (reader.ReadBoolean() != caseSensitive)
            throw new InvalidDataException("Different path case sensitivity");
          var data = new IndexCacheData { RootPath = reader.ReadString() };
          var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
          if (!string.Equals(data.RootPath, expectedRootPath, comparison))
            throw new InvalidDataException($"Root path \"{data.RootPath}\" instead of \"{expectedRootPath}\"");

          var arenaCount = reader.ReadInt32();
          if (arenaCount < 0)
            throw new InvalidDataException("Invalid arena count");
          for (var i = 0; i < arenaCount; i++) {
            var id = reader.ReadInt32();
            var used = reader.ReadInt32();
            if (id <= 0 || used <= 0 || used > ArenaSize || !data.Arenas.TryAdd(id, used))
              throw new InvalidDataException("Invalid arena table");
          }

          var entryCount = reader.ReadInt32();
          if (entryCount < 0)
            throw new InvalidDataException("Invalid entry count");
          data.Entries.Capacity = Math.Min(entryCount, 16 * 1024 * 1024);
          for (var i = 0; i < entryCount; i++) {
            var path = reader.ReadString();
            var ticks = reader.ReadInt64();
            var fileLength = reader.ReadInt64();
            var flags = (IndexCacheEntryFlags)reader.ReadByte();
            int arenaId = 0, offset = 0, length = 0;
            if ((flags & IndexCacheEntryFlags.Binary) == 0) {
              arenaId = reader.ReadInt32();
              offset = reader.ReadInt32();
              length = reader.ReadInt32();
              if (!data.Arenas.TryGetValue(arenaId, out var used) ||
                  offset < 0 || length < 0 || (offset % ArenaAllocator.Alignment) != 0 ||
                  (long)offset + length + TrailingByteCount > used)
                throw new InvalidDataException($"Invalid location of entry \"{path}\"");
            }
            if (path.Length == 0 || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || fileLength < 0)
              throw new InvalidDataException($"Invalid entry \"{path}\"");
            data.Entries.Add(new KeyValuePair<string, IndexCacheEntry>(path,
              new IndexCacheEntry(ticks, fileLength, flags, arenaId, offset, length)));
          }

          if (reader.ReadUInt32() != TrailerMagic || reader.ReadInt32() != entryCount)
            throw new InvalidDataException("Invalid trailer");
          if (stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected data after the trailer");
          return data;
        }
        catch (EndOfStreamException e) {
          throw new InvalidDataException("Truncated index", e);
        }
      }
    }
  }
}
