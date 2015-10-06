// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Features.BuildOutputAnalyzer;

namespace VsChromium.Tests.Features {
  [TestClass]
  public class TestBuildOutputAnalyzer {
    [TestMethod]
    public void BuildOutputParserWorks() {
      // Paths using various directory separators and prefixes
      AssertFullPathMatch(@"d:\src\tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"d:\src\tcp_socket_event_dispatcher.h", 10, -1);
      AssertFullPathMatch(@"d:/src/tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"d:/src/tcp_socket_event_dispatcher.h", 10, -1);
      AssertFullPathMatch(@"\\src\tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"\\src\tcp_socket_event_dispatcher.h", 10, -1);
      AssertFullPathMatch(@"//src/sockets_tcp/tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"//src/sockets_tcp/tcp_socket_event_dispatcher.h", 10, -1);
      AssertFullPathMatch(@"//src/tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"//src/tcp_socket_event_dispatcher.h", 10, -1);

      AssertFullPathMatch(@"d:\foo.txt : error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", -1, -1);

      // Path + line number
      AssertFullPathMatch(@"d:\foo.txt(11): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, -1);
      AssertFullPathMatch(@"d:\foo.txt (11): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, -1);

      // Path + line and column number
      AssertFullPathMatch(@"d:\foo.txt(11,5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);
      AssertFullPathMatch(@"d:\foo.txt (11,5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);
      AssertFullPathMatch(@"d:\foo.txt (11 ,5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);
      AssertFullPathMatch(@"d:\foo.txt (11, 5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);
      AssertFullPathMatch(@"d:\foo.txt (11 , 5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);

      // Path with spaces
      AssertFullPathMatch(@"d:\fo o\foo.txt: error C2061: syntax error : identifier 'ensions'", @"d:\fo o\foo.txt", -1, -1);
      AssertFullPathMatch(@"d:\fo o\foo.txt: error C2061: syntax error : identifier 'ensions'", @"d:\fo o\foo.txt", -1, -1);
      AssertFullPathMatch(@"d:\ba r\foo.txt: error C2061: syntax error : identifier 'ensions'", @"d:\ba r\foo.txt", -1, -1);

      AssertFullPathMatch(@"d:\fo o\foo.txt  (5, 10): error C2061: syntax error : identifier 'ensions'", @"d:\fo o\foo.txt", 4, 9);
      AssertFullPathMatch(@"d:\fo o.t xt\foo.txt (5): error C2061: syntax error : identifier 'ensions'", @"d:\fo o.t xt\foo.txt", 4, -1);
      AssertFullPathMatch(@"d:\ba r\foo.txt (5): error C2061: syntax error : identifier 'ensions'", @"d:\ba r\foo.txt", 4, -1);

      // Path after whitespaces
      AssertFullPathMatch(@" d:\foo.txt(10) : error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 9, -1, 1, 14);
      AssertFullPathMatch(@"  d:\foo.txt(10) : error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 9, -1, 2, 14);
    }

    [TestMethod]
    public void BuildOutputParserDoesNotMatchInvalidFilenames() {
      AssertNoMatch(@"");
      AssertNoMatch(@"c");
      AssertNoMatch(@"c:");
      AssertNoMatch(@"c:\");
      AssertNoMatch(@"c:\ foo.txt");
    }

    [TestMethod]
    public void BuildOutputParserWorksWithRelativePaths() {
      AssertRelativePathMatch(@"\src\tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"\src\tcp_socket_event_dispatcher.h", 10, -1);
      AssertRelativePathMatch(@"src\tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"src\tcp_socket_event_dispatcher.h", 10, -1);

      AssertRelativePathMatch(@"\src\tcp_socket_event_dispatcher.h : error C2061: syntax error : identifier 'ensions'", @"\src\tcp_socket_event_dispatcher.h", -1, -1);
      AssertRelativePathMatch(@"src\tcp_socket_event_dispatcher.h : error C2061: syntax error : identifier 'ensions'", @"src\tcp_socket_event_dispatcher.h", -1, -1);

      AssertRelativePathMatch(@"\src\tcp_socket_event_dispatcher.h(11, 5) : error C2061: syntax error : identifier 'ensions'", @"\src\tcp_socket_event_dispatcher.h", 10, 4);
      AssertRelativePathMatch(@"src\tcp_socket_event_dispatcher.h(11, 5) : error C2061: syntax error : identifier 'ensions'", @"src\tcp_socket_event_dispatcher.h", 10, 4);

      AssertRelativePathMatch(@"\src\tcp_socket_event_dispatcher.h() : error C2061: syntax error : identifier 'ensions'", @"\src\tcp_socket_event_dispatcher.h()", -1, -1);
      AssertRelativePathMatch(@"src\tcp_socket_event_dispatcher.h() : error C2061: syntax error : identifier 'ensions'", @"src\tcp_socket_event_dispatcher.h()", -1, -1);

    }

    private void AssertNoMatch(string text) {
      var parser = new BuildOutputParser();
      var result = parser.ParseFullPath(text);
      Assert.IsNull(result);
    }

    private static void AssertFullPathMatch(string text, string filename, int line, int column, int index = -1, int length = -1) {
      var parser = new BuildOutputParser();
      var result = parser.ParseFullPath(text);
      Assert.IsNotNull(result);
      Assert.AreEqual(filename, result.FileName);
      Assert.AreEqual(line, result.LineNumber);
      Assert.AreEqual(column, result.ColumnNumber);
      if (index >= 0)
        Assert.AreEqual(index, result.Index);
      if (length >= 0)
        Assert.AreEqual(length, result.Length);
    }

    private static void AssertRelativePathMatch(string text, string filename, int line, int column, int index = -1, int length = -1) {
      var parser = new BuildOutputParser();
      var result = parser.ParseFullOrRelativePath(text);
      Assert.IsNotNull(result);
      Assert.AreEqual(filename, result.FileName);
      Assert.AreEqual(line, result.LineNumber);
      Assert.AreEqual(column, result.ColumnNumber);
      if (index >= 0)
        Assert.AreEqual(index, result.Index);
      if (length >= 0)
        Assert.AreEqual(length, result.Length);
    }
  }
}
