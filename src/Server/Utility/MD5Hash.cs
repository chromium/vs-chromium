// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VsChromium.Core.Utility {
  public static class MD5Hash {
    public static string CreateHash(Stream stream) {
      using (var md5 = MD5.Create()) {
        byte[] hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
      }
    }

    public static string CreateHash(IEnumerable<string> lines) {
      // Create string from file
      var sb = new StringBuilder();
      foreach (var line in lines) {
        sb.Append(line);
        sb.Append("\r\n");
      }

      // Copy string to stream, and hash it.
      using (var stream = new MemoryStream()) {
        using (var writer = new StreamWriter(stream, Encoding.UTF8)) {
          writer.Write(sb.ToString());
          writer.Flush();
          stream.Position = 0;
          return CreateHash(stream);
        }
      }
    }
  }
}
