// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.IO;
using VsChromiumCore;
using VsChromiumCore.Win32.Files;
using VsChromiumCore.Win32.Strings;
using VsChromiumServer.VsChromiumNative;

namespace VsChromiumServer.Search {
  [Export(typeof(IFileContentsFactory))]
  public class FileContentsFactory : IFileContentsFactory {
    public FileContents GetFileContents(string path) {
      return ReadFile(path);
    }

    private FileContents ReadFile(string fullName) {
      try {
        var fileInfo = new SlimFileInfo(fullName);
        var heap = NativeFile.ReadFileNulTerminated(fileInfo);
        var textLen = (int)heap.ByteLength - 1;
        var kind = VsChromiumNative.NativeMethods.Text_GetKind(heap.Pointer, textLen);

        switch (kind) {
          case NativeMethods.TextKind.Ascii:
            return new AsciiFileContents(heap, 0, fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.AsciiWithUtf8Bom:
            const int utf8BomSize = 3;
            return new AsciiFileContents(heap, utf8BomSize, fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.Utf8WithBom:
            var utf16Contents = Conversion.UTF8ToUnicode(heap);
            heap.Dispose();
            return new UTF16FileContents(utf16Contents, fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.Unknown:
          default:
            // TODO(rpaquay): Figure out a better way to detect encoding.
            //Logger.Log("File \"{0}\" contains non-ascii characters.", fullName);
            return new AsciiFileContents(heap, 0, fileInfo.LastWriteTimeUtc);
            //throw new NotImplementedException(string.Format("Text Encoding of file \"{0}\" is not recognized.", fullName));
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Error reading content of text file \"{0}\", skipping file.", fullName);
        return StringFileContents.Empty;
      }
    }

    private static FileContents ReadFileAsUTF16FileContents(SlimFileInfo fileInfo) {
      using (var heap = NativeFile.ReadFileNulTerminated(fileInfo)) {
        var utf16Contents = Conversion.UTF8ToUnicode(heap);
        return new UTF16FileContents(utf16Contents, fileInfo.LastWriteTimeUtc);
      }
    }

    private static FileContents ReadFileAsStringFileContents(SlimFileInfo fileInfo) {
      var content = File.ReadAllText(fileInfo.FullName);
      return new StringFileContents(content);
    }
  }
}
