// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Win32.Strings;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.FileSystemContents {
  [Export(typeof(IFileContentsFactory))]
  public class FileContentsFactory : IFileContentsFactory {
    private readonly IFileSystem _fileSystem;
    private readonly ICache _cache = new WeakRefCache();
    /// <summary>
    /// Note: We use an instance variable to avoid creating delegate instances
    /// at every invocation of the cache.
    /// </summary>
    private readonly Func<IFileInfoSnapshot, FileContents> _creator;

    public interface ICache {
      FileContents GetOrAdd(IFileInfoSnapshot fileInfo, Func<IFileInfoSnapshot, FileContents> creator);
    }

    public class PassThroughCache : ICache {
      public FileContents GetOrAdd(IFileInfoSnapshot fileInfo, Func<IFileInfoSnapshot, FileContents> creator) {
        return creator(fileInfo);
      }
    }

    /// <summary>
    /// TODO(rpaquay) This is experimental only, as the key we use is not unique enough to make this cache reliable.
    /// </summary>
    public class WeakRefCache : ICache {
      private readonly Dictionary<MapKey, WeakReference<FileContents>> _map = new Dictionary<MapKey, WeakReference<FileContents>>();
      private readonly object _lock = new object();

      public FileContents GetOrAdd(IFileInfoSnapshot fileInfo, Func<IFileInfoSnapshot, FileContents> creator) {
        var key = new MapKey(fileInfo.Path.FileName, fileInfo.Length, fileInfo.LastWriteTimeUtc);
        var newFileContents = creator(fileInfo);
        var oldFileContents = LookupFileContents(key);
        if (oldFileContents != null && oldFileContents.HasSameContents(newFileContents)) {
          return oldFileContents;
        }
        return StoreFileContents(key, newFileContents);
      }

      private FileContents LookupFileContents(MapKey key) {
        lock (_lock) {
          WeakReference<FileContents> contents;
          if (_map.TryGetValue(key, out contents)) {
            FileContents fileContents;
            if (contents.TryGetTarget(out fileContents)) {
              return fileContents;
            }
          }
        }
        return null;
      }

      private FileContents StoreFileContents(MapKey key, FileContents value) {
        lock (_lock) {
          _map[key] = new WeakReference<FileContents>(value);
        }
        return value;
      }

      public struct MapKey : IEquatable<MapKey> {
        private readonly string _fileName;
        private readonly long _length;
        private readonly DateTime _lastWriteTimeUtc;

        public MapKey(string fileName, long length, DateTime lastWriteTimeUtc) {
          _fileName = fileName;
          _length = length;
          _lastWriteTimeUtc = lastWriteTimeUtc;
        }

        public string FileName {
          get { return _fileName; }
        }

        public long Length {
          get { return _length; }
        }

        public DateTime LastWriteTimeUtc {
          get { return _lastWriteTimeUtc; }
        }

        public bool Equals(MapKey other) {
          return
            this.Length == other.Length &&
            this.LastWriteTimeUtc == other.LastWriteTimeUtc &&
            SystemPathComparer.Instance.Comparer.Equals(this.FileName, other.FileName);
        }
      }
    }

    [ImportingConstructor]
    public FileContentsFactory(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
      _creator = this.ReadFileContents;
    }

    public FileContents GetFileContents(FullPath path) {
      var fileInfo = _fileSystem.GetFileInfoSnapshot(path);
      return _cache.GetOrAdd(fileInfo, _creator);
    }

    private FileContents ReadFileContents(IFileInfoSnapshot fileInfo) {
      try {
        const int trailingByteCount = 2;
        var block = _fileSystem.ReadFileNulTerminated(fileInfo, trailingByteCount);
        var contentsByteCount = (int)block.ByteLength - trailingByteCount; // Padding added by ReadFileNulTerminated
        var kind = NativeMethods.Text_GetKind(block.Pointer, contentsByteCount);

        switch (kind) {
          case NativeMethods.TextKind.Ascii:
            return new AsciiFileContents(new FileContentsMemory(block, 0, contentsByteCount), fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.AsciiWithUtf8Bom:
            const int utf8BomSize = 3;
            return new AsciiFileContents(new FileContentsMemory(block, utf8BomSize, contentsByteCount - utf8BomSize), fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.Utf8WithBom:
            var utf16Block = Conversion.UTF8ToUnicode(block);
            block.Dispose();
            return new UTF16FileContents(new FileContentsMemory(utf16Block, 0, utf16Block.ByteLength), fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.Unknown:
          default:
            // TODO(rpaquay): Figure out a better way to detect encoding.
            //Logger.Log("Text Encoding of file \"{0}\" is not recognized.", fullName);
            return new AsciiFileContents(new FileContentsMemory(block, 0, contentsByteCount), fileInfo.LastWriteTimeUtc);
          //throw new NotImplementedException(string.Format("Text Encoding of file \"{0}\" is not recognized.", fullName));
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Error reading content of text file \"{0}\", skipping file.", fileInfo.Path);
        return StringFileContents.Empty;
      }
    }
  }
}
