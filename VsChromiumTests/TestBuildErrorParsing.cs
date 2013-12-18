// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromiumPackage.Features.BuildErrors;

namespace VsChromiumTests {
  [TestClass]
  public class TestBuildErrorParsing {
    [TestMethod]
    public void MatchFileNameNameWorks() {
      // Paths using various directory separators and prefixes
      AssertIsMatch(@"d:\src\tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"d:\src\tcp_socket_event_dispatcher.h", 10, -1);
      AssertIsMatch(@"d:/src/tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"d:/src/tcp_socket_event_dispatcher.h", 10, -1);
      AssertIsMatch(@"\\src\tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"\\src\tcp_socket_event_dispatcher.h", 10, -1);
      AssertIsMatch(@"//src/sockets_tcp/tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"//src/sockets_tcp/tcp_socket_event_dispatcher.h", 10, -1);
      AssertIsMatch(@"//src/tcp_socket_event_dispatcher.h(11) : error C2061: syntax error : identifier 'ensions'", @"//src/tcp_socket_event_dispatcher.h", 10, -1);

      AssertIsMatch(@"d:\foo.txt : error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", -1, -1);

      // Path + line number
      AssertIsMatch(@"d:\foo.txt(11): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, -1);
      AssertIsMatch(@"d:\foo.txt (11): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, -1);

      // Path + line and column number
      AssertIsMatch(@"d:\foo.txt(11,5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);
      AssertIsMatch(@"d:\foo.txt (11,5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);
      AssertIsMatch(@"d:\foo.txt (11 ,5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);
      AssertIsMatch(@"d:\foo.txt (11, 5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);
      AssertIsMatch(@"d:\foo.txt (11 , 5): error C2061: syntax error : identifier 'ensions'", @"d:\foo.txt", 10, 4);

      // Path with spaces
      AssertIsMatch(@"d:\fo o.txt: error C2061: syntax error : identifier 'ensions'", @"d:\fo o.txt", -1, -1);
      AssertIsMatch(@"d:\fo o.t xt: error C2061: syntax error : identifier 'ensions'", @"d:\fo o.t xt", -1, -1);

      AssertIsMatch(@"d:\fo o.txt  (5, 10): error C2061: syntax error : identifier 'ensions'", @"d:\fo o.txt", 4, 9);
      AssertIsMatch(@"d:\fo o.t xt (5): error C2061: syntax error : identifier 'ensions'", @"d:\fo o.t xt", 4, -1);
    }

    private static void AssertIsMatch(string text, string filename, int line, int column) {
      var parser = new BuildOutputParser();
      var result = parser.ParseLine(text);
      Assert.IsNotNull(result);
      Assert.AreEqual(filename, result.FileName);
      Assert.AreEqual(line, result.LineNumber);
      Assert.AreEqual(column, result.ColumnNumber);
    }
  }
}
