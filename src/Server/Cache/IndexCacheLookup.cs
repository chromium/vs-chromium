// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Cache {
  /// <summary>
  /// The entries of the index of the cache of a project (<see cref="IndexCacheIndex"/>), with
  /// lookups by relative path (span, no allocation) and their mapped arena files. Compact: an open
  /// addressing hash table of file indices (no string nor object per entry).
  /// </summary>
  public sealed class IndexCacheLookup {
    private static readonly char Separator = PathHelpers.DirectorySeparatorChar;
    private readonly IndexCacheIndex _index;
    private readonly StringComparison _comparison;
    /// <summary>File index + 1 (0: empty), linear probing.</summary>
    private readonly int[] _buckets;
    private readonly int _mask;
    private readonly Dictionary<int, MappedArena> _arenas;

    public IndexCacheLookup(IndexCacheIndex index, IEnumerable<MappedArena> arenas, bool caseSensitive) {
      _index = index;
      _comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
      _arenas = arenas.ToDictionary(x => x.Id);
      var size = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(16, index.Files.Length * 2));
      _buckets = new int[size];
      _mask = size - 1;

      // Note: The hash of a file is the hash of its relative path (same as the lookups).
      var buffer = new char[1024];
      var files = index.Files;
      var currentDirectory = -1;
      var directoryLength = 0;
      for (var i = 0; i < files.Length; i++) {
        ref var file = ref files[i];
        if (file.Directory != currentDirectory) {
          currentDirectory = file.Directory;
          var directory = index.GetDirectoryPath(currentDirectory);
          directoryLength = directory.Length == 0 ? 0 : directory.Length + 1;
          EnsureLength(ref buffer, directoryLength);
          directory.CopyTo(buffer);
          if (directoryLength > 0)
            buffer[directoryLength - 1] = Separator;
        }
        var name = index.GetName(i);
        EnsureLength(ref buffer, directoryLength + name.Length);
        name.CopyTo(buffer.AsSpan(directoryLength));
        var path = buffer.AsSpan(0, directoryLength + name.Length);
        var bucket = string.GetHashCode(path, _comparison) & _mask;
        while (true) {
          var existing = _buckets[bucket] - 1;
          if (existing < 0) {
            _buckets[bucket] = i + 1;
            Count++;
            break;
          }
          if (Matches(existing, path))
            break; // Duplicate (e.g. names differing only by case): the first one wins.
          bucket = (bucket + 1) & _mask;
        }
      }
    }

    private static void EnsureLength(ref char[] buffer, int length) {
      if (buffer.Length < length)
        Array.Resize(ref buffer, Math.Max(length, buffer.Length * 2));
    }

    public int Count { get; }

    /// <summary>The index (and tree) of the cache.</summary>
    public IndexCacheIndex Index => _index;

    /// <summary>The managed memory of the lookup (approximate).</summary>
    public long ByteSize => _index.ByteSize + (long)_buckets.Length * sizeof(int);

    private bool Matches(int file, ReadOnlySpan<char> path) {
      var directory = _index.GetDirectoryPath(_index.Files[file].Directory);
      var name = _index.GetName(file);
      if (directory.Length == 0)
        return path.Equals(name, _comparison);
      return path.Length == directory.Length + 1 + name.Length &&
             path[directory.Length] == Separator &&
             path.Slice(0, directory.Length).Equals(directory, _comparison) &&
             path.Slice(directory.Length + 1).Equals(name, _comparison);
    }

    private int Find(ReadOnlySpan<char> path) {
      var bucket = string.GetHashCode(path, _comparison) & _mask;
      while (true) {
        var file = _buckets[bucket] - 1;
        if (file < 0)
          return -1;
        if (Matches(file, path))
          return file;
        bucket = (bucket + 1) & _mask;
      }
    }

    /// <summary>
    /// The contents of the file of <paramref name="relativePath"/> if the cache has an entry with
    /// the time and size of <paramref name="metadata"/>, otherwise null.
    /// </summary>
    public FileContents TryGetContents(ReadOnlySpan<char> relativePath, FileMetadata metadata) {
      var file = Find(relativePath);
      return file < 0 ? null : CreateContents(file, metadata);
    }

    public FileContents TryGetContents(FileName fileName, FileMetadata metadata) {
      var file = fileName.UseRelativePath(static (path, lookup) => lookup.Find(path), this);
      return file < 0 ? null : CreateContents(file, metadata);
    }

    /// <summary>
    /// Validation of an entry: same last write time and size as the scan (rule of the git index).
    /// Known limitation (Windows): the scan has the metadata of the directory entries, which NTFS
    /// doesn't update for the other names of a hard link modified through one of its names, and the
    /// metadata of the link itself for file symbolic links (not of the target): if such a file was
    /// cached while its directory entry was up to date, a later change through another name (or of
    /// the target of a symbolic link) is not detected ("Rebuild index" reads all the files).
    /// </summary>
    private FileContents CreateContents(int file, FileMetadata metadata) {
      var entry = _index.Files[file];
      if ((entry.Flags & (IndexCacheEntryFlags.NoContents | IndexCacheEntryFlags.NotSearchable)) != 0)
        return null;
      if (entry.LastWriteTimeTicks != metadata.LastWriteTimeUtc.Ticks || entry.FileLength != metadata.Length)
        return null;
      var lastWriteTimeUtc = new DateTime(entry.LastWriteTimeTicks, DateTimeKind.Utc);
      if ((entry.Flags & IndexCacheEntryFlags.Binary) != 0) {
        return new BinaryFileContents(lastWriteTimeUtc, entry.FileLength);
      }
      var arena = _arenas[entry.ArenaId];
      return new Utf8FileContents(new FileContentsMemory(arena.Handle, arena.Size, entry.Offset, entry.Length),
        lastWriteTimeUtc, (entry.Flags & IndexCacheEntryFlags.Ascii) != 0, entry.FileLength);
    }
  }
}
