// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// VsChromiumNative.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"

#include <assert.h>
#include <stdlib.h>

#include "search_bndm32.h"
#include "search_bndm64.h"
#include "search_boyer_moore.h"
#include "search_strstr.h"
#include "search_regex.h"

#define EXPORT __declspec(dllexport)

template<class CharType>
bool GetLineExtentFromPosition(const CharType* text, int textLen, int position, int* lineStartPosition, int* lineLen) {
  const CharType nl = '\n';
  const CharType* min = text;
  const CharType* max = text + textLen;
  const CharType* current = text + position;

  const CharType* start = current;
  for (; start > min; start--) {
    if (*start == nl) {
      break;
    }
  }

  const CharType* end = current;
  for (; end < max; end++) {
    if (*end == nl) {
      break;
    }
  }

  assert(min <= start);
  assert(start <= max);
  assert(min <= end);
  assert(end <= max);

  // TODO(rpaquay): We are limited to 2GB for now.
  *lineStartPosition = static_cast<int>(start - min);
  *lineLen = static_cast<int>(end - start);
  return true;
}

extern "C" {

enum SearchAlgorithmKind {
  kStrStr = 1,
  kBndm32 = 2,
  kBndm64 =3,
  kBoyerMoore = 4,
  kRegex,
};

EXPORT AsciiSearchBase* __stdcall AsciiSearchAlgorithm_Create(
    SearchAlgorithmKind kind, const char* pattern, int patternLen, AsciiSearchBase::SearchOptions options) {
  AsciiSearchBase* result = NULL;

  switch(kind) {
    case kBndm32:
      if (options & AsciiSearchBase::kMatchCase)
        result = new Bndm32Search<CaseSensitive>();
      else
        result = new Bndm32Search<CaseInsensitive>();
      break;
    case kBndm64:
      if (options & AsciiSearchBase::kMatchCase)
        result = new Bndm64Search<CaseSensitive>();
      else
        result = new Bndm64Search<CaseInsensitive>();
      break;
    case kBoyerMoore:
      result = new BoyerMooreSearch();
      break;
    case kStrStr:
      result = new StrStrSearch();
      break;
    case kRegex:
      result = new RegexSearch();
      break;
  }

  if (!result)
    return result;

  bool success = result->PreProcess(pattern, patternLen, options);
  if (!success) {
    delete result;
    return NULL;
  }

  return result;
}

EXPORT void __stdcall AsciiSearchAlgorithm_Search(
    AsciiSearchBase* search,
    AsciiSearchBase::SearchParams* searchParams) {
  search->Search(searchParams);
}

EXPORT void __stdcall AsciiSearchAlgorithm_Delete(AsciiSearchBase* search) {
  delete search;
}

enum TextKind {
  Ascii,
  AsciiWithUtf8Bom,
  Utf8WithBom,
  Unknown
};

namespace {

bool Text_HasUtf8Bom(const char *text, int textLen) {
  return textLen >= 3 &&
    text[0] == 0xEF &&
    text[1] == 0xBB &&
    text[2] == 0xBF;
}

bool Text_IsAscii(const char* text, int textLen) {
  const uint8_t* textPtr = (const uint8_t *)text;
  const uint8_t* textEndPtr = textPtr + textLen;
  const uint8_t asciiLimit = 0x7f;
  for(; textPtr < textEndPtr; textPtr++) {
    if (*textPtr > asciiLimit)
      return false;
  }
  return true;
}

}

EXPORT TextKind __stdcall Text_GetKind(const char* text, int textLen) {
  bool utf8 = Text_HasUtf8Bom(text, textLen);
  if (utf8) {
    bool isAscii = Text_IsAscii(text + 3, textLen -3);
    if (isAscii)
      return AsciiWithUtf8Bom;
    else
      return Utf8WithBom;
  } else {
    bool isAscii = Text_IsAscii(text, textLen);
    if (isAscii)
      return Ascii;
    else
      return Unknown;
  }
}

EXPORT bool __stdcall Ascii_Compare(const char *text1, size_t text1Length, const char* text2, size_t text2Length) {
  if (text1Length != text2Length)
    return false;

  return memcmp(text1, text2, text1Length) == 0;
}

EXPORT bool __stdcall Ascii_GetLineExtentFromPosition(const char* text, int textLen, int position, int* lineStartPosition, int* lineLen) {
  return GetLineExtentFromPosition(text, textLen, position, lineStartPosition, lineLen);
}

EXPORT bool __stdcall UTF16_GetLineExtentFromPosition(const wchar_t* text, int textLen, int position, int* lineStartPosition, int* lineLen) {
  return GetLineExtentFromPosition(text, textLen, position, lineStartPosition, lineLen);
}

}  // extern "C"
