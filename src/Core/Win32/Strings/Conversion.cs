// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Win32.Strings {
  public static class Conversion {
    public static unsafe SafeHeapBlockHandle UTF8ToUnicode(SafeHeapBlockHandle block) {
      var start = (byte*)block.Pointer;
      var end = start + block.ByteLength;
      return UTF8ToUnicode(start, end);
    }

    public static unsafe SafeHeapBlockHandle UTF8ToUnicode(byte* start, byte* end) {
      if (start >= end)
        throw new ArgumentException();

      var length = new IntPtr(end - start).ToInt64();
      if (length >= Int32.MaxValue)
        throw new ArgumentException("Size limited to Int32 size.");

      var byteLength = (int)length;
      var decoder = Encoding.UTF8.GetDecoder();
      var charCount = decoder.GetCharCount(start, byteLength, true);
      var newBlock = HeapAllocStatic.Alloc(charCount * 2);
      var result = decoder.GetChars(start, byteLength, (char*)newBlock.Pointer.ToPointer(),
                                    charCount, true);
      if (result != charCount) {
        throw new Exception("Error converting UTF8 string to UTF16 string.");
      }
      return newBlock;
    }

    public static unsafe string UTF8ToString(byte* start, byte* end) {
      var block = UTF8ToUnicode(start, end);
      return new string((char*)block.Pointer, 0, (int)block.ByteLength / 2);
    }

    public static unsafe string UnicodeToUnicode(byte[] bytes) {
      fixed (void* pointer = bytes) {
        return new string((char*)pointer);
      }
    }

    public static unsafe string AnsiToUnicode(byte[] bytes) {
      fixed (byte* pointer = bytes) {
        return new string((sbyte*)pointer);
      }
    }
  }
}
