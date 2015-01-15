// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#include "stdafx.h"

#include "search_strstr.h"

StrStrSearch::StrStrSearch()
    : pattern_(NULL),
      patternLen_(0) {
}

bool StrStrSearch::PreProcess(const char *pattern, int patternLen, SearchOptions options) {
  pattern_ = pattern;
  patternLen_ = patternLen;
  return true;
}

void StrStrSearch::Search(const char *text, int textLen, Callback matchFound) {
  const char* end = text + textLen;
  while(true) {
    const char* str = strstr(text, pattern_);
    if (str == nullptr)
      break;

    if (!matchFound(str, patternLen_))
      break;

    text = str + patternLen_;
    textLen = (int)(end - text);
  }
}
