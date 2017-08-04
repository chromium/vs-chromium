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
    private readonly Action<string> _printer;
    private readonly List<ColumnInfo> _columns = new List<ColumnInfo>();

    public TextTableGenerator(Action<string> printer) {
      if (printer == null)
        throw new ArgumentNullException("printer");

      _printer = printer;
    }

    public void AddColumn(string header, int width, Align align, Stringifier stringifier) {
      if (header == null)
        throw new ArgumentNullException("header");

      if (width <= 0)
        throw new ArgumentException("Width should be a positive number", "width");

      if (!Enum.IsDefined(typeof(Align), align))
        throw new ArgumentException("Invalid alignment value", "align");

      if (stringifier == null)
        throw new ArgumentNullException("stringifier");

      _columns.Add(new ColumnInfo {
        Header = header,
        Width = width,
        Align = align,
        Stringifier = stringifier,
      });
    }

    public void GenerateReport(IEnumerable<IEnumerable<object>> valueProvider) {
      if (valueProvider == null)
        throw new ArgumentNullException("valueProvider");

      _printer(RowSeparator);
      _printer(RowHeader);
      _printer(RowSeparator);
      foreach (var rowValues in valueProvider) {
        var e = rowValues.GetEnumerator();
        string rowText = ProduceRow((i, c) => {
          if (e.MoveNext()) { 
            return c.Stringifier(c, e.Current);
          }
          // This is really a contract violation
          return "";
        });
        _printer(rowText);
      }
      _printer(RowSeparator);
    }

    /// <summary>
    /// Function that produces a string representation of a column value
    /// </summary>
    public delegate string Stringifier(ColumnInfo columnInfo, object value);

    /// <summary>
    /// Column alignment
    /// </summary>
    public enum Align {
      Left,
      Right,
    }

    /// <summary>
    /// Characteristics of a column of the table
    /// </summary>
    public class ColumnInfo {
      public string Header { get; set; }
      public int Width { get; set; }
      public Align Align { get; set; }
      public Stringifier Stringifier { get; set; }
    }

    public static class Stringifiers {

      public static Stringifier RegularString {
        get {
          return (c, x) => x == null ? "" : x.ToString();
        }
      }

      public static Stringifier EllipsisString {
        get {
          return (c, x) => {
            string text = RegularString(c, x);
            if (text.Length > c.Width && c.Width >= 5 && text.Length >= 5) {
              int count = (c.Width - 3) / 2;
              return Left(text, count) + "..." + Right(text, c.Width - count - 3);
            }
            return x.ToString();
          };
        }
      }

      /// <summary>
      /// See https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#NFormatString
      /// </summary>
      public static Stringifier DecimalGroupedInteger {
        get {
          return (c, x) => x == null ? "" : string.Format("{0:n0}", x);
        }
      }

      private static string Left(string value, int count) {
        return value.Substring(0, count);
      }

      private static string Right(string value, int count) {
        return value.Substring(value.Length - count);
      }
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
