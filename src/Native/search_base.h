// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#pragma once

#include <stdint.h>

class AsciiSearchBase {
 public:
  enum SearchOptions {
    kMatchCase = 0x0001
  };

  AsciiSearchBase();
  virtual ~AsciiSearchBase();

  virtual bool PreProcess(const char *pattern, int patternLen, SearchOptions options) = 0;
  virtual const char* Search(const char *text, int texLen) = 0;

  static const uint8_t read_byte(const uint8_t* text, int index, bool matchCase) {
    uint8_t value = text[index];
    if (matchCase)
      return value;

    if (value >= 'A' && value <= 'Z')
      value |= 0x20;
    return value;
  }

 protected:
  enum { kAlphabetLen = 256 };
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
