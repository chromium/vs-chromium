// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.FileSystemContents;

namespace VsChromium.Tests.Server {
  public static class Utils {
    public static FileContentsMemory CreateAsciiMemory(string text) {
      var size = text.Length + 1;
      var handle = new SafeHGlobalHandle(Marshal.StringToHGlobalAnsi(text));
      return new FileContentsMemory(handle, size, 0, size - 1);
    }

    public static FileContentsMemory CreateUtf16Memory(string text) {
      var size = (text.Length + 1) * sizeof(char);
      var handle = new SafeHGlobalHandle(Marshal.StringToHGlobalUni(text));
      return new FileContentsMemory(handle, size, 0, size - 1);
    }

    public static AsciiFileContents CreateAsciiFileContents(string text) {
      var memory = CreateAsciiMemory(text);
      var contents = new AsciiFileContents(memory, DateTime.Now);
      return contents;
    }

    public static Utf16FileContents CreateUtf16FileContents(string text) {
      var memory = CreateUtf16Memory(text);
      var contents = new Utf16FileContents(memory, DateTime.Now);
      return contents;
    }

    public static FileContentsHash CreateFileContentsHash(string text) {
      var mem = CreateAsciiMemory(text);
      var hash = new FileContentsHash(mem);
      return hash;
    }
  }
}
