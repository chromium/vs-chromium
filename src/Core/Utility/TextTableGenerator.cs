// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Text;

namespace VsChromium.Core.Utility {
  /// <summary>
  /// Helper class to generate text based output for multi-colum tables,
  /// including alignment, padding and custom ToString functions.
  ///
  /// The output format is as follows:
  /// 
  /// <code>
  /// + --------- + ---------- +
  /// + Header    + Header2    +
  /// + --------- + ---------- +
  /// + Value     + Value      +
  /// + Value     + Value      +
  /// + --------- + ---------- +
  /// </code>
  /// 
  /// </summary>
  public class TextTableGenerator {
    private readonly List<ColumnInfo> _columns = new List<ColumnInfo>();
    private Action<string> _outputer;

    public TextTableGenerator(Action<string> outputer) {
      _outputer = outputer;
    }

    public class ColumnInfo {
      public string Header { get; set; }
      public int Width { get; set; }
      public Align Align { get; set; }
      public Func<object, string> Stringifier { get; set; }
    }

    public enum Align {
      Left,
      Right,
    }

    public void AddColumn(string header, int width, Align align, Func<object, string> stringifier) {
      _columns.Add(new ColumnInfo {
        Header = header,
        Width = width,
        Align = align,
        Stringifier = stringifier,
      });
    }

    public void GenerateReport(IEnumerable<List<object>> valueProvider) {
      _outputer(RowSeparator);
      _outputer(RowHeader);
      _outputer(RowSeparator);
      foreach (var list in valueProvider) {
        _outputer(ProduceRow((i, c) => c.Stringifier(list[i])));
      }
      _outputer(RowSeparator);
    }

    public static class Stringifiers {
      public static Func<Object, string> ForString { get { return x => x.ToString(); } }
      public static Func<Object, string> ForFormattedNumber { get { return x => string.Format("{0:n0}", x); } }
    }

    private string RowHeader {
      get {
        return ProduceRow((i, c) => c.Header);
      }
    }

    private string RowSeparator {
      get {
        return ProduceRow((i, c) => new string('-', c.Width));
      }
    }

    private string ProduceRow(Func<int, ColumnInfo, string> valueProvider) {
      StringBuilder sb = new StringBuilder();
      if (_columns.Count > 0) {
        sb.Append("+ ");
        for (int i = 0; i < _columns.Count; i++) {
          if (i > 0) {
            sb.Append("| ");
          }
          sb.Append(PadString(_columns[i], valueProvider(i, _columns[i])));
          sb.Append(" ");
        }
        sb.Append("+");
      }
      return sb.ToString();
    }

    private string PadString(ColumnInfo columnInfo, string value) {
      int width = columnInfo.Width;
      if (value.Length >= width) {
        return value.Substring(0, width);
      }

      string padding = new string(' ', width - value.Length);
      switch(columnInfo.Align) {
        case Align.Left:
          return value + padding;
        case Align.Right:
          return padding + value;
        default:
          throw new InvalidOperationException();
      }
    }
  }
}
