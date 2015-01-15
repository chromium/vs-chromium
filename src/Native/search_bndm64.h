// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#pragma once

#include <stdint.h>
#include <stdlib.h>
#include <assert.h>

#include "search_base.h"

template<typename T>
class Bndm64Search : public AsciiSearchBaseTemplate<T> {
 public:
  Bndm64Search()
      : pattern_(NULL),
        patternLen_(0) {
    memset(maskv_, 0, sizeof(maskv_));
  }

  bool PreProcess(const char *pattern, int patternLen, SearchOptions options) OVERRIDE {
    assert(patternLen <= 64);

    pattern_ = pattern;
    patternLen_ = patternLen;
    uint8_t *pat = (uint8_t*)pattern;
    for (int i = 0; i < patternLen; ++i)
      setbit64(&maskv_[Traits::FetchByte(pat, i)], patternLen - 1 - i);
    return true;
  }

  virtual void Search(SearchParams* searchParams) OVERRIDE {
    const char* text = searchParams->TextStart;
    int textLen = searchParams->TextLength;
    if (searchParams->MatchStart != nullptr) {
      text = searchParams->MatchStart + searchParams->MatchLength;
      // TODO(rpaquay): 2GB Limit
      textLen = (int)(searchParams->TextStart + searchParams->TextLength - text);
    }

    searchParams->MatchStart = bndm64_algo(text, textLen, pattern_, patternLen_, maskv_);
    if (searchParams->MatchStart != nullptr) {
      searchParams->MatchLength = patternLen_;
    }
  }

 private:
  // setbit: set a bit in a LSB-first 64bit word in memory.
  static void setbit64(uint64_t *v, int p) {
    assert(p >= 0);
    assert(p <= 63);

    uint64_t one = 1;
    v[p >> 6] |= one << (p & 63);
  }

  static const char *bndm64_algo(const char *text, int textLen,
                                 const char *pattern, int patternLen,
                                 uint64_t* maskv) {
    uint8_t *tgt = (uint8_t*)text;
    int j;

    for (int i = 0; i <= textLen - patternLen; i += j) {
      uint64_t mask = maskv[Traits::FetchByte(tgt, i + patternLen - 1)];
      for (j = patternLen; mask;) {
        if (!--j) return text + i;
        mask = (mask << 1) & maskv[Traits::FetchByte(tgt, i + j - 1)];
      }
    }

    return NULL;
  }

  const char *pattern_;
  int patternLen_;
  uint64_t maskv_[kAlphabetLen];
};
