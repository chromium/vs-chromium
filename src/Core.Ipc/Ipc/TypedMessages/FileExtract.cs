// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class FileExtract {
    /// <summary>
    /// The extracted text
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// The character offset of the extracted text.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// The number of characters in extracted text.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// The line number of the extracted text.
    /// </summary>
    public int LineNumber { get; set; }

    public int ColumnNumber { get; set; }
  }
}
