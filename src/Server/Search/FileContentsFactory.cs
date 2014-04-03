// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Core;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Strings;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  [Export(typeof(IFileContentsFactory))]
  public class FileContentsFactory : IFileContentsFactory {
    public FileContents GetFileContents(string path) {
      return ReadFile(path);
    }

    private FileContents ReadFile(string fullName) {
      try {
        var fileInfo = new SlimFileInfo(fullName);
        var block = NativeFile.ReadFileNulTerminated(fileInfo);
        var textLen = (int)block.ByteLength - 2; // Padding added by ReadFileNulTerminated
        var kind = NativeMethods.Text_GetKind(block.Pointer, textLen);

        switch (kind) {
          case NativeMethods.TextKind.Ascii:
            return new AsciiFileContents(block, 0, fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.AsciiWithUtf8Bom:
            const int utf8BomSize = 3;
            return new AsciiFileContents(block, utf8BomSize, fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.Utf8WithBom:
            var utf16Contents = Conversion.UTF8ToUnicode(block);
            block.Dispose();
            return new UTF16FileContents(utf16Contents, fileInfo.LastWriteTimeUtc);

          case NativeMethods.TextKind.Unknown:
          default:
            // TODO(rpaquay): Figure out a better way to detect encoding.
            //Logger.Log("Text Encoding of file \"{0}\" is not recognized.", fullName);
            return new AsciiFileContents(block, 0, fileInfo.LastWriteTimeUtc);
            //throw new NotImplementedException(string.Format("Text Encoding of file \"{0}\" is not recognized.", fullName));
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Error reading content of text file \"{0}\", skipping file.", fullName);
        return StringFileContents.Empty;
      }
    }
  }
}
