using System;
using System.Collections.Concurrent;
using VsChromium.Server.FileSystemContents;

namespace VsChromium.Server.FileSystemDatabase {
  public class FileContentsMemoization : IFileContentsMemoization {
    private readonly ConcurrentDictionary<MapKey, FileContents> _map = new ConcurrentDictionary<MapKey, FileContents>();

    public FileContents Get(FileData fileData, FileContents fileContents) {
      var key = new MapKey(fileData, fileContents);
      return _map.GetOrAdd(key, fileContents);
    }

    private struct MapKey : IEquatable<MapKey> {
      private readonly FileContents _fileContents;
      private readonly int _hashCode;

      public MapKey(FileData fileData, FileContents fileContents) {
        _fileContents = fileContents;
        _hashCode =
          CombineHashCodes(
            fileData.FileName.RelativePath.FileName.GetHashCode(),
            CombineHashCodes(
              (int)(fileContents.ByteLength & uint.MaxValue),
              (int)(fileContents.ByteLength >> 32)));
      }

      private static int CombineHashCodes(int h1, int h2) {
        unchecked {
          return (h1 << 5) + h1 ^ h2;
        }
      }

      public bool Equals(MapKey other) {
        return _fileContents.HasSameContents(other._fileContents);
      }

      public override int GetHashCode() {
        return _hashCode;
      }
    }
  }
}