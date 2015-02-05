// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#pragma once

#include <stdint.h>

class AsciiSearchBase {
 public:
  enum SearchOptions {
    // Search is case sensitive
    kMatchCase = 0x0001,
    kMatchWholeWord = 0x0002,
  };

  struct SearchParams {
    const char* TextStart;
    int TextLength;
    const char* MatchStart;
    int MatchLength;
    void* SearchBuffer;
  };

  struct SearchCreateResult {
    SearchCreateResult() : HResult(S_OK) {
      ErrorMessage[0] = 0;
    }
    void SetError(HRESULT hr, const char* message) {
      this->HResult = E_OUTOFMEMORY;
      strcpy_s(this->ErrorMessage, message);
    }
    HRESULT HResult;
    char ErrorMessage[128];
  };

  AsciiSearchBase();
  virtual ~AsciiSearchBase();

  void StartSearch(const char *pattern, int patternLen, SearchOptions options, SearchCreateResult& result);
  void FindNext(SearchParams* searchParams);
  virtual void CancelSearch(SearchParams* searchParams) {}
  virtual int GetSearchBufferSize() { return 0; }

  static const uint8_t read_byte(const uint8_t* text, int index, bool matchCase) {
    uint8_t value = text[index];
    if (matchCase)
      return value;

    if (value >= 'A' && value <= 'Z')
      value |= 0x20;
    return value;
  }

protected:
  virtual void StartSearchWorker(const char *pattern, int patternLen, SearchOptions options, SearchCreateResult& result) = 0;
  virtual void FindNextWorker(SearchParams* searchParams) = 0;

  enum { kAlphabetLen = 256 };

private:
  void FindNextWholeWord(SearchParams* searchParams);

private:
  typedef void (AsciiSearchBase::*FindNextFunction)(SearchParams* searchParams);
  FindNextFunction findNext_;
};

template <typename T>
struct AsciiSearchBaseTemplateCaseSensitive {
  static const uint8_t FetchByte(const uint8_t* text, int index);
};

struct CaseSensitive {};
struct CaseInsensitive {};

template <>
struct AsciiSearchBaseTemplateCaseSensitive<CaseSensitive> {
  static const uint8_t FetchByte(const uint8_t* text, int index) {
    return text[index];
  }
};

template <>
struct AsciiSearchBaseTemplateCaseSensitive<CaseInsensitive> {
  static const uint8_t FetchByte(const uint8_t* text, int index) {
    uint8_t value = text[index];
    return (value >= 'A' && value <= 'Z') ? value |= 0x20 : value;
  }
};

template <typename T, typename TTraits = AsciiSearchBaseTemplateCaseSensitive<T> >
class AsciiSearchBaseTemplate : public AsciiSearchBase {
 public:
  typedef TTraits Traits;
};
