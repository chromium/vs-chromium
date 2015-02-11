// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Text;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Win32.Strings {
  public static class Conversion {
    public static unsafe SafeHeapBlockHandle Utf8ToUtf16(SafeHeapBlockHandle block) {
      var start = (byte*)block.Pointer;
      var end = start + block.ByteLength;
      return Utf8ToUtf16(start, end);
    }

    public static unsafe SafeHeapBlockHandle Utf8ToUtf16(byte* start, byte* end) {
      if (start >= end)
        throw new ArgumentException();

      var length = new IntPtr(end - start).ToInt64();
      if (length >= Int32.MaxValue)
        throw new ArgumentException("Size limited to Int32 size.");

      var byteLength = (int)length;
      var decoder = Encoding.UTF8.GetDecoder();
      var charCount = decoder.GetCharCount(start, byteLength, true);
      var newBlock = HeapAllocStatic.Alloc(charCount * sizeof(char));
      var result = decoder.GetChars(start, byteLength, (char*)newBlock.Pointer.ToPointer(),
                                    charCount, true);
      if (result != charCount) {
        throw new Exception("Error converting UTF8 string to UTF16 string.");
      }
      return newBlock;
    }

    public static unsafe SafeHeapBlockHandle Utf16ToUtf8(char* start, char* end) {
      if (start >= end)
        throw new ArgumentException();

      var charCount = new IntPtr(end - start).ToInt64();
      if (charCount >= Int32.MaxValue)
        throw new ArgumentException("Size limited to Int32 size.");

      var encoder = Encoding.UTF8.GetEncoder();
      var byteCount = encoder.GetByteCount(start, (int)charCount, true);
      var newBlock = HeapAllocStatic.Alloc(byteCount);
      var result = encoder.GetBytes(start, (int)charCount, (byte*)newBlock.Pointer.ToPointer(),
                                    byteCount, true);
      if (result != byteCount) {
        throw new Exception("Error converting string from UTF16 to UTF8.");
      }
      return newBlock;
    }

    public static unsafe SafeHeapBlockHandle StringToUtf8(string value) {
      if (value == null)
        throw new ArgumentException();

      fixed(char* start = value) {
        var block = Utf16ToUtf8(start, start + value.Length);
        return block;
      }
    }

    public static unsafe string Utf8ToString(byte* start, byte* end) {
      return new string((sbyte*)start, 0, (int)(end - start));
    }

    public static unsafe string Utf8ToString(byte[] bytes) {
      // Note: When debugging unit tests, the following line will sometime
      // throw an exception of type "AccessViolationException" from 
      // VsChromium.Core.Debugger.DebuggerThread.GetOutputDebugString()
      // The only workaround found so far is to disable the call (see comment
      // in the method above).
      fixed (byte* pointer = bytes) {
        return new string((sbyte*)pointer, 0, bytes.Length);
      }
    }

    public static unsafe string Utf16ToString(byte* start, byte* end) {
      return new string((char*)start, 0, (int)((end - start) / sizeof(char)));
    }

    public static unsafe string Utf16ToString(byte[] bytes) {
      fixed (void* pointer = bytes) {
        return new string((char*)pointer, 0, bytes.Length / sizeof(char));
      }
    }
  }
}
