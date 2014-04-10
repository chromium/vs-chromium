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

const char *StrStrSearch::Search(const char *text, int textLen) {
  return strstr(text, pattern_);
}
