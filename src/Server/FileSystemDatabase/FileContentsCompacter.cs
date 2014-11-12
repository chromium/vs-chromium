using System.Collections.Generic;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystemContents;

namespace VsChromium.Server.FileSystemDatabase {
  public class FileContentsCompacter {
    public void Compact(FileNameDictionary<FileDatabaseBuilder.FileInfo> files) {
      var map = new Dictionary<FileData, FileContents>(new FileDataComparer());
      int count = 0;
      files.Values.ForAll(fileInfo => {
        if (fileInfo.FileData.Contents == null)
          return;
        var fileData = fileInfo.FileData;
        count++;
        FileContents value;
        if (map.TryGetValue(fileData, out value)) {
          fileData.UpdateContents(value);
        } else {
          map.Add(fileData, fileData.Contents);
        }
      });

      Logger.Log("Compacted {0:n0} files contents entries into {1:n0} unique entries.", count, map.Count);
    }

    /// <summary>
    /// Custom comparer used to merge identical file contents into a single
    /// dictionary entry.
    /// </summary>
    public class FileDataComparer : IEqualityComparer<FileData> {
      public bool Equals(FileData x, FileData y) {
        return x.Contents.HasSameContents(y.Contents);
      }

      public int GetHashCode(FileData obj) {
        // Note: We don't really care about collisions, we just need something
        // that is fast and "good enough" to avoid too many collisions.
        return
          CombineHashCodes(
            obj.FileName.RelativePath.FileName.GetHashCode(),
            CombineHashCodes(
              (int)(obj.Contents.ByteLength & uint.MaxValue),
              (int)(obj.Contents.ByteLength >> 32)));
      }

      private int CombineHashCodes(int h1, int h2) {
        unchecked {
          return (h1 << 5) + h1 ^ h2;
        }
      }
    }
  }
}