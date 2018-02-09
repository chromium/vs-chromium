// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  public class BinaryFileContents : FileContents {
    private readonly long _byteLength;

    private static readonly Lazy<FileContentsMemory> LazyEmptyContentsMemory = new Lazy<FileContentsMemory>(() => {
      var block = HeapAllocStatic.Alloc(1);
      Marshal.WriteByte(block.Pointer, 0);
      return new FileContentsMemory(block, 0, 0);
    });

    private static readonly Lazy<BinaryFileContents> LazyEmpty = new Lazy<BinaryFileContents>(() => 
      new BinaryFileContents(DateTime.MinValue, -1));
    
    public static BinaryFileContents Empty {
      get { return LazyEmpty.Value; }
    }

    public BinaryFileContents(DateTime utcLastModified, long byteLength)
      : base(LazyEmptyContentsMemory.Value, utcLastModified) {
      _byteLength = byteLength;
    }

    protected override ITextLineOffsets GetFileOffsets() {
      throw new NotImplementedException();
    }

    public override byte CharacterSize {
      get { return 1; }
    }

    public long BinaryFileSize {
      get { return _byteLength; }
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchContainer container) {
      return NullCompiledTextSearch.Instance;
    }

    protected override TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength) {
      return new TextRange(0, 0);
    }
  }
}