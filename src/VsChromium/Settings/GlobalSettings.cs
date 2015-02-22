// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel;
using VsChromium.Core.Utility;

namespace VsChromium.Settings {
  public class GlobalSettings : INotifyPropertyChanged {
    private bool _enableVsChromiumProjects;
    private int _maxTextExtractLength;
    private int _searchFileNamesMaxResults;
    private int _findInFilesMaxEntries;
    private int _autoSearchDelayMsec;
    private bool _searchMatchCase;
    private bool _searchMatchWholeWord;
    private bool _searchUseRegEx;
    private bool _searchIncludeSymLinks;
    private bool _codingStyleAccessorIndent;
    private bool _codingStyleTrailingSpace;
    private bool _codingStyleTabCharacter;
    private bool _codingStyleSpaceAfterForKeyword;
    private bool _codingStyleOpenBraceAfterNewLine;
    private bool _codingStyleLongLine;
    private bool _codingStyleEndOfLineCharacter;
    private bool _codingStyleElseIfOnNewLine;

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
      var handler = PropertyChanged;
      if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
    }

    public bool EnableVsChromiumProjects {
      get { return _enableVsChromiumProjects; }
      set {
        if (value == _enableVsChromiumProjects)
          return;

        _enableVsChromiumProjects = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x=> x.EnableVsChromiumProjects));
      }
    }

    public int MaxTextExtractLength {
      get { return _maxTextExtractLength; }
      set {
        if (value == _maxTextExtractLength)
          return;

        _maxTextExtractLength = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.MaxTextExtractLength));

      }
    }

    public int SearchFileNamesMaxResults {
      get { return _searchFileNamesMaxResults; }
      set {
        if (value == _searchFileNamesMaxResults)
          return;

        _searchFileNamesMaxResults = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.SearchFileNamesMaxResults));

      }
    }

    public int FindInFilesMaxEntries {
      get { return _findInFilesMaxEntries; }
      set {
        if (value == _findInFilesMaxEntries)
          return;

        _findInFilesMaxEntries = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.FindInFilesMaxEntries));

      }
    }

    public int AutoSearchDelayMsec {
      get { return _autoSearchDelayMsec; }
      set {
        if (value == _autoSearchDelayMsec)
          return;

        _autoSearchDelayMsec = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.AutoSearchDelayMsec));
      }
    }

    public bool SearchMatchCase {
      get { return _searchMatchCase; }
      set {
        if (value == _searchMatchCase)
          return;

        _searchMatchCase = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.SearchMatchCase));
      }
    }

    public bool SearchMatchWholeWord {
      get { return _searchMatchWholeWord; }
      set {
        if (value == _searchMatchWholeWord)
          return;

        _searchMatchWholeWord = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.SearchMatchWholeWord));
      }
    }

    public bool SearchUseRegEx {
      get { return _searchUseRegEx; }
      set {
        if (value == _searchUseRegEx)
          return;

        _searchUseRegEx = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.SearchUseRegEx));
      }
    }

    public bool SearchIncludeSymLinks {
      get { return _searchIncludeSymLinks; }
      set {
        if (value == _searchIncludeSymLinks)
          return;

        _searchIncludeSymLinks = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.SearchIncludeSymLinks));
      }
    }

    #region Chromium Coding Style

    public bool CodingStyleAccessorIndent {
      get { return _codingStyleAccessorIndent; }
      set {
        if (value == _codingStyleAccessorIndent)
          return;

        _codingStyleAccessorIndent = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CodingStyleAccessorIndent));
      }
    }

    public bool CodingStyleElseIfOnNewLine {
      get { return _codingStyleElseIfOnNewLine; }
      set {
        if (value == _codingStyleElseIfOnNewLine)
          return;

        _codingStyleElseIfOnNewLine = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CodingStyleElseIfOnNewLine));
      }
    }

    public bool CodingStyleEndOfLineCharacter {
      get { return _codingStyleEndOfLineCharacter; }
      set {
        if (value == _codingStyleEndOfLineCharacter)
          return;

        _codingStyleEndOfLineCharacter = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CodingStyleEndOfLineCharacter));
      }
    }

    public bool CodingStyleLongLine {
      get { return _codingStyleLongLine; }
      set {
        if (value == _codingStyleLongLine)
          return;

        _codingStyleLongLine = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CodingStyleLongLine));
      }
    }

    public bool CodingStyleOpenBraceAfterNewLine {
      get { return _codingStyleOpenBraceAfterNewLine; }
      set {
        if (value == _codingStyleOpenBraceAfterNewLine)
          return;

        _codingStyleOpenBraceAfterNewLine = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CodingStyleOpenBraceAfterNewLine));
      }
    }

    public bool CodingStyleSpaceAfterForKeyword {
      get { return _codingStyleSpaceAfterForKeyword; }
      set {
        if (value == _codingStyleSpaceAfterForKeyword)
          return;

        _codingStyleSpaceAfterForKeyword = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CodingStyleSpaceAfterForKeyword));
      }
    }

    public bool CodingStyleTabCharacter {
      get { return _codingStyleTabCharacter; }
      set {
        if (value == _codingStyleTabCharacter)
          return;

        _codingStyleTabCharacter = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CodingStyleTabCharacter));
      }
    }

    public bool CodingStyleTrailingSpace {
      get { return _codingStyleTrailingSpace; }
      set {
        if (value == _codingStyleTrailingSpace)
          return;

        _codingStyleTrailingSpace = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.CodingStyleTrailingSpace));
      }
    }

    #endregion
  }
}