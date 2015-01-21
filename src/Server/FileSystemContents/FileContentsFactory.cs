// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Win32.Strings;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.FileSystemContents {
  [Export(typeof(IFileContentsFactory))]
  public class FileContentsFactory : IFileContentsFactory {
    private readonly IFileSystem _fileSystem;

    [ImportingConstructor]
    public FileContentsFactory(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public FileContents GetFileContents(FullPath path) {
      try {
        var fileInfo = _fileSystem.GetFileInfoSnapshot(path);
        return ReadFileContents(fileInfo);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error reading content of text file \"{0}\", skipping file.", path);
        return AsciiFileContents.Empty;
      }
    }

    private FileContents ReadFileContents(IFileInfoSnapshot fileInfo) {
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
          return new Utf16FileContents(new FileContentsMemory(utf16Block, 0, utf16Block.ByteLength), fileInfo.LastWriteTimeUtc);

        case NativeMethods.TextKind.Unknown:
        default:
          // TODO(rpaquay): Figure out a better way to detect encoding.
          //Logger.Log("Text Encoding of file \"{0}\" is not recognized.", fullName);
          return new AsciiFileContents(new FileContentsMemory(block, 0, contentsByteCount), fileInfo.LastWriteTimeUtc);
        //throw new NotImplementedException(string.Format("Text Encoding of file \"{0}\" is not recognized.", fullName));
      }
    }
  }
}
