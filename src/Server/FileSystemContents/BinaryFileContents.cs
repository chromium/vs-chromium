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
    private static readonly Lazy<FileContentsMemory> LazyEmptyContentsMemory = new Lazy<FileContentsMemory>(() => {
      var block = HeapAllocStatic.Alloc(1);
      Marshal.WriteByte(block.Pointer, 0);
      return new FileContentsMemory(block, 0, 0);
    });

    private static readonly Lazy<BinaryFileContents> LazyEmpty = new Lazy<BinaryFileContents>(() => 
      new BinaryFileContents(DateTime.MinValue));
    
    public static BinaryFileContents Empty {
      get { return LazyEmpty.Value; }
    }

    public BinaryFileContents(DateTime utcLastModified)
      : base(LazyEmptyContentsMemory.Value, utcLastModified) {
    }

    public override bool HasSameContents(FileContents other) {
      return other is BinaryFileContents;
    }

    protected override ITextLineOffsets GetFileOffsets() {
      throw new NotImplementedException();
    }

    protected override byte CharacterSize {
      get { return 1; }
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchContainer container) {
      return NullCompiledTextSearch.Instance;
    }

    protected override TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength) {
      return new TextRange(0, 0);
    }
  }
}