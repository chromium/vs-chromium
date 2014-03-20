// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#pragma once

#include <stdint.h>
#include <stdlib.h>
#include <assert.h>

#include "search_base.h"

template<typename T>
class Bndm32Search : public AsciiSearchBaseTemplate<T> {
 public:
  Bndm32Search()
      : pattern_(NULL),
        patternLen_(0) {
    memset(maskv_, 0, sizeof(maskv_));
  }

  bool PreProcess(const char *pattern, int patternLen, SearchOptions options) OVERRIDE {
    assert(patternLen <= 32);

    pattern_ = pattern;
    patternLen_ = patternLen;
    uint8_t *pat = (uint8_t*)pattern;
    for (int i = 0; i < patternLen; ++i)
      setbit32(&maskv_[Traits::FetchByte(pat, i)], patternLen - 1 - i);
    return true;
  }

  virtual const char *Search(const char *text, int textLen) OVERRIDE {
    return bndm32_algo(text, textLen, pattern_, patternLen_, maskv_);
  }

 private:
  // setbit: set a bit in a LSB-first 32bit word in memory.
  static void setbit32(uint32_t *v, int p) {
    assert(p >= 0);
    assert(p <= 31);

    uint32_t one = 1;
    v[p >> 5] |= one << (p & 31);
  }

  static const char *bndm32_algo(const char *text, int textLen,
                                 const char *pattern, int patternLen,
                                 uint32_t* maskv) {
    uint8_t *tgt = (uint8_t*)text;
    int j;

    for (int i = 0; i <= textLen - patternLen; i += j) {
      uint32_t mask = maskv[Traits::FetchByte(tgt, i + patternLen - 1)];
      for (j = patternLen; mask;) {
        if (!--j) return text + i;
        mask = (mask << 1) & maskv[Traits::FetchByte(tgt, i + j - 1)];
      }
    }

    return NULL;
  }

  const char *pattern_;
  int patternLen_;
  uint32_t maskv_[kAlphabetLen];
};
