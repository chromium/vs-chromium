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
bool GetLineExtentFromPosition(
    const CharType* text,
    int textLen,
    int position,
    int maxOffset,
    int* lineStartPosition,
    int* lineLen) {
  const CharType nl = '\n';
  const CharType* low = max(text, text + position - maxOffset);
  const CharType* high = min(text + textLen, text + position + maxOffset);
  const CharType* current = text + position;

  // Search backward up to "min"
  const CharType* start = current;
  for (; start > low; start--) {
    if (*start == nl) {
      break;
    }
  }

  // Search forward up to "max"
  const CharType* end = current;
  for (; end < high; end++) {
    if (*end == nl) {
      break;
    }
  }

  assert(low <= start);
  assert(start <= high);
  assert(low <= end);
  assert(end <= high);

  // TODO(rpaquay): We are limited to 2GB for now.
  *lineStartPosition = static_cast<int>(start - text);
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
    SearchAlgorithmKind kind,
    const char* pattern,
    int patternLen,
    AsciiSearchBase::SearchOptions options, 
    AsciiSearchBase::SearchCreateResult* searchCreateResult) {
  (*searchCreateResult) = AsciiSearchBase::SearchCreateResult();
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

  if (!result) {
    searchCreateResult->SetError(E_OUTOFMEMORY, "Out of memory");
    return result;
  }

  result->PreProcess(pattern, patternLen, options, *searchCreateResult);
  if (FAILED(searchCreateResult->HResult)) {
    delete result;
    return NULL;
  }

  return result;
}

EXPORT int __stdcall AsciiSearchAlgorithm_GetSearchBufferSize(AsciiSearchBase* search) {
  return search->GetSearchBufferSize();
}

EXPORT void __stdcall AsciiSearchAlgorithm_Search(
    AsciiSearchBase* search,
    AsciiSearchBase::SearchParams* searchParams) {
  search->Search(searchParams);
}

EXPORT void __stdcall AsciiSearchAlgorithm_CancelSearch(
    AsciiSearchBase* search,
    AsciiSearchBase::SearchParams* searchParams) {
  search->CancelSearch(searchParams);
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

EXPORT bool __stdcall Ascii_GetLineExtentFromPosition(
    const char* text,
    int textLen,
    int position,
    int maxOffset,
    int* lineStartPosition,
    int* lineLen) {
  return GetLineExtentFromPosition(
      text, textLen, position, maxOffset, lineStartPosition, lineLen);
}

EXPORT bool __stdcall UTF16_GetLineExtentFromPosition(
    const wchar_t* text,
    int textLen,
    int position,
    int maxOffset,
    int* lineStartPosition,
    int* lineLen) {
  return GetLineExtentFromPosition(
      text, textLen, position, maxOffset, lineStartPosition, lineLen);
}

}  // extern "C"
